"""根据构建清单打包 Windows 便携预览版，避免带入旧插件和用户配置。"""

from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


def main():
    root = Path(__file__).resolve().parents[1]
    output = root / "src/.artifacts/Debug"
    solution = ET.parse(root / "src/STranslate.slnx").getroot()
    projects = [Path(item.attrib["Path"]).stem for item in solution.iter("Project")]
    files = set()

    for project in projects:
        if project == "STranslate.Tests":
            continue
        manifests = list((root / "src/.artifacts/obj" / project).rglob("*.FileListAbsolute.txt"))
        if not manifests:
            raise RuntimeError(f"未找到构建清单，请先构建 Debug 解决方案：{project}")
        for manifest in manifests:
            for line in manifest.read_text(encoding="utf-8-sig").splitlines():
                path = Path(line)
                if not path.is_file() or not path.is_relative_to(output) or path.suffix == ".pdb":
                    continue
                relative = path.relative_to(output)
                if any(part in ("Cache", "Settings", "Logs", "PortableConfig") for part in relative.parts):
                    raise RuntimeError(f"构建清单中出现用户数据：{relative}")
                if ".Tts." in str(relative) or ".Vocabulary." in str(relative):
                    raise RuntimeError(f"构建清单中出现已移除的插件：{relative}")
                files.add(path)

    readme = """STranslate 个人精简版（Debug 预览包）

适用于 Windows x64，需要 .NET 10 Desktop Runtime。

1. 完整解压到独立目录，保留空的 PortableConfig 文件夹。
2. 退出正在运行的原版 STranslate，再运行 STranslate.exe。
3. 首次启动请配置并启用翻译服务；截图翻译还需要启用 OCR 服务。
4. 默认划词后显示小图标，点击后翻译。截图翻译快捷键为 Alt + S。
5. 生词本和语音合成相关入口已移除，翻译与 OCR 引擎保留。

便携配置独立存储，无需覆盖原版安装目录。
不要安装程序内提示的上游版本更新，以免覆盖精简版。
编译和自动化测试结果见本次 GitHub Actions 运行页面。
自动化测试不能代替实际划词悬浮窗、截图及翻译请求的 Windows 交互验证。
详见“精简版说明.md”。
"""

    publish = root / "publish"
    publish.mkdir(exist_ok=True)
    archive = publish / "STranslate-Simplify-win-x64-preview.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for path in sorted(files):
            package.write(path, path.relative_to(output).as_posix())
        package.writestr("PortableConfig/", "")
        package.writestr("README_试用说明.txt", readme.encode("utf-8-sig"))
        package.write(root / "src/docs/simplified-edition.md", "精简版说明.md")
        package.write(root / "LICENSE", "LICENSE")

    with zipfile.ZipFile(archive) as package:
        corrupt_file = package.testzip()
        if corrupt_file:
            raise RuntimeError(f"ZIP 完整性检查失败：{corrupt_file}")
        names = set(package.namelist())
        required = [
            "STranslate.exe", "STranslate.dll", "STranslate.Plugin.dll",
            "STranslate.runtimeconfig.json", "STranslate.deps.json",
            "z_stranslate_host.exe", "PortableConfig/",
            "Plugins/STranslate.Plugin.Ocr.WeChatBuiltIn/wco_data/WeChatOCR.exe",
        ]
        for project in projects:
            if project.startswith("STranslate.Plugin."):
                required.extend([f"Plugins/{project}/plugin.json", f"Plugins/{project}/{project}.dll"])
        for name in required:
            if name not in names:
                raise RuntimeError(f"预览包缺少必要文件：{name}")

    print(f"已打包：{archive}（{archive.stat().st_size / 1024 / 1024:.1f} MiB）")


if __name__ == "__main__":
    main()
