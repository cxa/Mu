namespace Counter.Droid

open System

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Mu
open Counter.Core
open MainComponent

[<Activity (Label = "Counter.Droid",
            MainLauncher = true,
            Icon = "@mipmap/icon")>]
type MainActivity () =
  inherit Activity ()

  override x.OnCreate (bundle) =
    base.OnCreate (bundle)
    x.SetContentView (Resources.Layout.Main)
    MainComponent.runInView x

  interface Mu.IView<Model, Action.T> with
    member x.BindModel model binder =
      let countLabel = x.FindViewById<TextView> Resources.Id.countLabel
      binder.Bind <@ model.Number @> (fun n ->
        countLabel.Text <- sprintf "%d" n)
      let randomButton = x.FindViewById<Button> Resources.Id.randomButton
      binder.Bind <@ model.ButtonTitle @> (fun t ->
        randomButton.Text <- t)
      let descLabel = x.FindViewById<TextView> Resources.Id.descLabel
      binder.Bind <@ model.Message @> (fun m ->
        descLabel.Text <- m)

    member x.BindAction send =
      let incrButton = x.FindViewById<Button> Resources.Id.incrButton
      incrButton.Click.Add (fun _ -> send Action.Incr)
      let decrButton = x.FindViewById<Button> Resources.Id.decrButton
      decrButton.Click.Add (fun _ -> send Action.Decr)
      let randomButton = x.FindViewById<Button> Resources.Id.randomButton
      randomButton.Click.Add (fun _ -> send Action.HandleRandomizing)
