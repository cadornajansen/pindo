using System.Text.Json;
using LocalTutor.Core;

namespace LocalTutor.Tests;

public sealed class TutorContractsTests
{
    [Fact]
    public void TutorMessagesRoundTripWithUnknownStateAndNegativeScreenCoordinates()
    {
        UiElementSnapshot element = new(
            "save-button",
            "Save",
            "Button",
            new ScreenRectangle(-1920, -80, 120, 36));
        TutorRequest request = new("Save the document", "Example editor", [element]);
        TutorResponse response = new(null, "No matching target was found.", false, "Target unavailable.");

        TutorRequest? restoredRequest = JsonSerializer.Deserialize<TutorRequest>(JsonSerializer.Serialize(request));
        TutorResponse? restoredResponse = JsonSerializer.Deserialize<TutorResponse>(JsonSerializer.Serialize(response));

        Assert.NotNull(restoredRequest);
        Assert.Equal(request.Instruction, restoredRequest.Instruction);
        Assert.Equal(request.ActiveApplicationName, restoredRequest.ActiveApplicationName);
        Assert.Equal(element, Assert.Single(restoredRequest.Elements));
        Assert.Null(restoredRequest.Elements[0].IsEnabled);
        Assert.Equal(response, restoredResponse);
        Assert.Null(restoredResponse!.TargetElementId);
    }
}
