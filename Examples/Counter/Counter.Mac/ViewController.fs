namespace Counter.Mac

open System

open Foundation
open AppKit

open Mu
open Counter.Core
open MainComponent

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
  inherit NSViewController (handle)

  [<Outlet>] member val countLabel:NSTextField = null with get, set
  [<Outlet>] member val incrButton:NSButton = null with get, set
  [<Outlet>] member val decrButton:NSButton = null with get, set
  [<Outlet>] member val randomButton:NSButton = null with get, set
  [<Outlet>] member val descLabel:NSTextField = null with get, set

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    MainComponent.runInView x

  interface IView<Model, Event.T> with
    member x.BindModel model binder =
      binder.Bind <@ model.Number @> (fun n -> 
        x.countLabel.StringValue <- sprintf "%d" n
      )
      binder.Bind <@ model.Message @> (fun m ->
        x.descLabel.StringValue <- m
      )

    member x.BindEvent emit =
      x.incrButton.Activated.Add (fun _ -> emit Event.Incr)
      x.decrButton.Activated.Add (fun _ -> emit Event.Decr)
      x.randomButton.Activated.Add (fun _ -> emit Event.RequestRandom)
