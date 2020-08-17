namespace Counter.Core

module MainComponent =
  open Mu
  open System.Threading

  type Model =
    { Number: int
      Requesting: bool
      RequestingTokenSource: CancellationTokenSource option
      ButtonTitle: string
      Message: string }

  module Msg =
    type T =
      | Incr
      | Decr
      | HandleRandomizing
      | RequestRandomYear
      | UserCancel
      | ReceiveRandomYear of Result<string, exn>

  module private Cmds =
    let requestRandomNumber tkSource =
      use wc = new System.Net.WebClient()
      let task = wc.DownloadStringTaskAsync "http://numbersapi.com/random/year"
      let ofSucc str = Msg.ReceiveRandomYear (Ok str)
      let ofErr err = Msg.ReceiveRandomYear (Error err)
      Cmd.OfTask.either' task ofSucc ofErr tkSource

  let init() =
    { Number = System.Random().Next 100
      Requesting = false
      RequestingTokenSource = None
      ButtonTitle = "Randomize"
      Message = "" }

  let update model action =
    match action with
    | Msg.Incr ->
      { model with Number = model.Number + 1 }, Cmd.none

    | Msg.Decr ->
      { model with Number = model.Number - 1 }, Cmd.none

    | Msg.HandleRandomizing ->
      let msg =
        if model.Requesting
        then Msg.UserCancel
        else Msg.RequestRandomYear
      model, Cmd.ofMsg msg

    | Msg.RequestRandomYear ->
      let newModel =
        { model with
            Requesting = true
            RequestingTokenSource = Some (new CancellationTokenSource ())
            ButtonTitle = "Cancel"
            Message = "Requesting a random year..." }
      newModel, Cmds.requestRandomNumber (Option.get newModel.RequestingTokenSource)

    | Msg.UserCancel ->
      let newModel =
        { model with
            Requesting = false
            ButtonTitle = "Randomize"
            Message = "" }
      let cancel () =
        model.RequestingTokenSource |> Option.iter (fun s -> s.Cancel())
      newModel, Cmd.OfFunc.eff cancel

    | Msg.ReceiveRandomYear(Ok str) ->
      let number = str.Split(' ') |> Array.head |> int // unsafe here
      let newModel =
        { model with
            RequestingTokenSource = None
            Requesting = false
            ButtonTitle = "Randomize"
            Number = number
            Message = str }
      newModel, Cmd.none

    | Msg.ReceiveRandomYear(Error err) ->
      let newModel =
        { model with
            RequestingTokenSource = None
            Requesting = false
            ButtonTitle = "Randomize"
            Message = err.ToString() }
      newModel, Cmd.none

  let runInView view = Mu.run init update view
