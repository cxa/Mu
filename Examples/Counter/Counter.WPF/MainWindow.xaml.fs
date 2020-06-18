namespace Counter.WPF

open FsXaml
open Mu
open Counter.Core
open MainComponent

type MainWindowBase = XAML<"MainWindow.xaml">

type MainWindow() as this =
  inherit MainWindowBase()
  do this.Loaded.Add(fun _ -> MainComponent.runInView this)

  interface IView<Model, Msg.T> with

    member x.BindModel model =
      <@ x.countLabel.Content <- sprintf "%d" model.Number
         x.randButton.Content <- model.ButtonTitle
         x.descTextBlock.Text <- model.Message @>

    member x.BindMsg send =
      x.incrButton.Click.Add(fun _ -> send Msg.Incr)
      x.decrButton.Click.Add(fun _ -> send Msg.Decr)
      x.randButton.Click.Add(fun _ -> send Msg.HandleRandomizing)
