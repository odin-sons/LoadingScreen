namespace LoadingScreen.Tests;

using LoadingScreen.Model;
using Xunit;

public class LoadingSessionTests {
  [Fact]
  public void GeneralJoinKeepsTheSameSelectionAndCaptionAcrossBothHandovers() {
    LoadingSession session = new();
    Selection selected = new() { Entry = new Entry { Image = "b.jpg" } };
    session.Record(LoadingStage.Menu, selected, "caption", "entry text", 5f);
    session.MarkVisible(5f);

    Assert.True(session.CanContinue(LoadingStage.Scene, Situation.General));
    session.Continue(LoadingStage.Scene);
    Assert.True(session.CanContinue(LoadingStage.Hud, Situation.General));
    session.Continue(LoadingStage.Hud);

    Assert.Same(selected, session.Selection);
    Assert.Equal("b.jpg", session.Selection.Entry.Image);
    Assert.Equal("caption", session.Caption);
    Assert.Equal("entry text", session.CurrentCaption);
    Assert.Equal(5f, session.StartedAt);
  }

  [Fact]
  public void HandoverKeepsTheLastRenderedAnimationTime() {
    LoadingSession session = new();
    session.Record(LoadingStage.Menu, new Selection { Entry = new Entry() }, null, null, 3f);
    session.MarkVisible(3f);
    session.Tick(1.2f);
    session.Continue(LoadingStage.Hud);

    Assert.Equal(1.2f, session.AnimationElapsed);

    session.Record(LoadingStage.Hud, new Selection { Entry = new Entry() }, null, null, 20f);
    Assert.Equal(0f, session.AnimationElapsed);
  }

  [Fact]
  public void NewSituationOrSeparateLoadCannotContinue() {
    LoadingSession session = new();
    session.Record(LoadingStage.Scene, new Selection { Entry = new Entry() }, "old", null, 1f);

    Assert.False(session.CanContinue(LoadingStage.Hud, Situation.Sleep));
    Assert.False(session.CanContinue(LoadingStage.Scene, Situation.General));
    Assert.True(session.CanContinue(LoadingStage.Hud, Situation.General));

    session.Reset();
    Assert.False(session.CanContinue(LoadingStage.Hud, Situation.General));
  }

  [Fact]
  public void MenuZoomBeginsWhenLoadingBecomesVisible() {
    LoadingSession session = new();
    session.Record(LoadingStage.Menu, new Selection { Entry = new Entry() }, "initial", null, 1f);

    Assert.False(session.CanContinue(LoadingStage.Scene, Situation.General));
    session.MarkVisible(10f);
    session.SetCaption("rotated");

    Assert.True(session.CanContinue(LoadingStage.Scene, Situation.General));
    Assert.Equal(10f, session.StartedAt);
    Assert.Equal("rotated", session.Caption);
  }

  [Fact]
  public void ImagePoolChoiceIsReusedOnlyUntilNextSelection() {
    LoadingSession session = new();
    Layer layer = new();
    int picks = 0;

    Assert.Equal("image-1", session.ImageFor(layer, () => $"image-{++picks}"));
    Assert.Equal("image-1", session.ImageFor(layer, () => $"image-{++picks}"));
    session.ResetImages();
    Assert.Equal("image-2", session.ImageFor(layer, () => $"image-{++picks}"));
  }
}
