namespace Counter.IOS

open System

open Foundation
open UIKit

open Mu
open Counter.Core
open MainComponent

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
  inherit UIViewController (handle)

  [<Outlet>] member val countLabel:UILabel = null with get, set
  [<Outlet>] member val incrButton:UIButton = null with get, set
  [<Outlet>] member val decrButton:UIButton = null with get, set
  [<Outlet>] member val randButton:UIButton = null with get, set
  [<Outlet>] member val descLabel:UILabel = null with get, set

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    MainComponent.runInView x

  interface IView<Model, Action.T> with
    member x.BindModel model binder =
      binder.Bind <@ model.Number @> (fun n ->
        x.countLabel.Text <- sprintf "%d" n)
      binder.Bind <@ model.ButtonTitle @> (fun t ->
        x.randButton.SetTitle(t, UIControlState.Normal))
      binder.Bind <@ model.Message @> (fun m ->
        x.descLabel.Text <- m)

    member x.BindAction send =
      x.incrButton.TouchUpInside.Add (fun _ -> send Action.Incr)
      x.decrButton.TouchUpInside.Add (fun _ -> send Action.Decr)
      x.randButton.TouchUpInside.Add (fun _ -> send Action.HandleRandomizing)
