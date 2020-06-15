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
    member x.BindModel model =
      <@
      x.countLabel.Text <- sprintf "%d" model.Number
      x.randButton.SetTitle(model.ButtonTitle, UIControlState.Normal)
      x.descLabel.Text <- model.Message
      @>

    member x.BindAction send =
      x.incrButton.TouchUpInside.Add (fun _ -> send Action.Incr)
      x.decrButton.TouchUpInside.Add (fun _ -> send Action.Decr)
      x.randButton.TouchUpInside.Add (fun _ -> send Action.HandleRandomizing)
