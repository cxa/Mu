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

[<Activity(Label = "Counter.Droid", MainLauncher = true, Icon = "@mipmap/icon")>]
type MainActivity() =
  inherit Activity()

  override x.OnCreate(bundle) =
    base.OnCreate(bundle)
    x.SetContentView(Resources.Layout.Main)
    MainComponent.runInView x

  interface Mu.IView<Model, Msg.T> with

    member x.BindModel model =
      let countLabel = x.FindViewById<TextView> Resources.Id.countLabel
      let randomButton = x.FindViewById<Button> Resources.Id.randomButton
      let descLabel = x.FindViewById<TextView> Resources.Id.descLabel
      <@ countLabel.Text <- sprintf "%d" model.Number
         descLabel.Text <- model.Message
         randomButton.Text <- model.ButtonTitle @>

    member x.BindMsg send =
      let incrButton = x.FindViewById<Button> Resources.Id.incrButton
      incrButton.Click.Add(fun _ -> send Msg.Incr)
      let decrButton = x.FindViewById<Button> Resources.Id.decrButton
      decrButton.Click.Add(fun _ -> send Msg.Decr)
      let randomButton = x.FindViewById<Button> Resources.Id.randomButton
      randomButton.Click.Add(fun _ -> send Msg.HandleRandomizing)
