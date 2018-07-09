namespace Counter.WPF

open FsXaml
open Mu
open Counter.Core
open MainComponent

type MainWindowBase = XAML<"MainWindow.xaml">

type MainWindow () =
  inherit MainWindowBase ()

  interface IView<Model, Action.T> with
    member x.BindModel model binder =
      binder.Bind <@ model.Number @> (fun n ->
        x.countLabel.Content <- sprintf "%d" n)
      binder.Bind <@ model.ButtonTitle @> (fun t ->
        x.randButton.Content <- t)
      binder.Bind <@ model.Message @> (fun m ->
        x.descTextBlock.Text <- m)

    member x.BindAction send =
      x.incrButton.Click.Add (fun _ -> send Action.Incr)
      x.decrButton.Click.Add (fun _ -> send Action.Decr)
      x.randButton.Click.Add (fun _ -> send Action.HandleRandomizing)
      