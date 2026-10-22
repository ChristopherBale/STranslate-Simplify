using STranslate.Core;
using System.Text.Json;

namespace STranslate.Tests;

public class SimplifiedSettingsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ExistingMouseSelectionPreferencesOverrideNewDefaults(bool direct, bool icon)
    {
        var json = JsonSerializer.Serialize(new
        {
            IsMouseSelectionTranslationEnabled = direct,
            IsMouseSelectionIconEnabled = icon
        });

        var settings = JsonSerializer.Deserialize<Settings>(json)!;

        Assert.Equal(direct, settings.IsMouseSelectionTranslationEnabled);
        Assert.Equal(icon, settings.IsMouseSelectionIconEnabled);
    }

    [Fact]
    public void SavingServiceSettingsPreservesLegacyServiceConfiguration()
    {
        const string json = """
            {
              "TranSvcDatas": [{ "SvcID": "translate", "Name": "Microsoft", "IsEnabled": true }],
              "OcrSvcDatas": [{ "SvcID": "ocr", "Name": "WeChat", "IsEnabled": true }],
              "TtsSvcDatas": [{ "SvcID": "tts", "Name": "Edge", "IsEnabled": true }],
              "VocabularySvcDatas": [{ "SvcID": "words", "Name": "Eudict", "IsEnabled": false }]
            }
            """;

        var settings = JsonSerializer.Deserialize<ServiceSettings>(json)!;
        settings.TranSvcDatas[0].Name = "My translator";
        var restored = JsonSerializer.Deserialize<ServiceSettings>(JsonSerializer.Serialize(settings))!;

        Assert.Equal("My translator", Assert.Single(restored.TranSvcDatas).Name);
        Assert.Equal("ocr", Assert.Single(restored.OcrSvcDatas).SvcID);
        Assert.Equal("tts", Assert.Single(restored.TtsSvcDatas).SvcID);
        var vocabulary = Assert.Single(restored.VocabularySvcDatas);
        Assert.Equal("words", vocabulary.SvcID);
        Assert.False(vocabulary.IsEnabled);
    }
}
