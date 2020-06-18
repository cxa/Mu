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

  module private Effects =
    let requestRandom tkSource _model =
      async {
        use wc = new System.Net.WebClient()
        let! choice = wc.DownloadStringTaskAsync "http://numbersapi.com/random/year"
                      |> Async.AwaitTask
                      |> Async.Catch
        let result =
          match choice with
          | Choice1Of2 str -> Ok str
          | Choice2Of2 err -> Error err
        return (Msg.ReceiveRandomYear result)
      }, tkSource

  let init() =
    { Number = System.Random().Next 100
      Requesting = false
      RequestingTokenSource = None
      ButtonTitle = "Randomize"
      Message = "" }

  let update model action =
    match action with
    | Msg.Incr -> Update { model with Number = model.Number + 1 }
    | Msg.Decr -> Update { model with Number = model.Number - 1 }
    | Msg.HandleRandomizing ->
        let cmd =
          Cmd(fun model ->
            if model.Requesting then Msg.UserCancel else Msg.RequestRandomYear)
        Effects cmd
    | Msg.RequestRandomYear ->
        let ts = new CancellationTokenSource()
        UpdateWithEffects
          ({ model with
               Requesting = true
               RequestingTokenSource = Some ts
               ButtonTitle = "Cancel"
               Message = "Requesting a random year..." }, Cmd''(Effects.requestRandom ts))
    | Msg.UserCancel ->
        UpdateWithEffects
          ({ model with
               Requesting = false
               ButtonTitle = "Randomize"
               Message = "" },
           Eff(fun model _send ->
             model.RequestingTokenSource |> Option.iter (fun s -> s.Cancel())))
    | Msg.ReceiveRandomYear(Ok str) ->
        let number =
          str.Split(' ')
          |> Array.head
          |> int // unsafe here
        Update
          { model with
              RequestingTokenSource = None
              Requesting = false
              ButtonTitle = "Randomize"
              Number = number
              Message = str }
    | Msg.ReceiveRandomYear(Error err) ->
        Update
          { model with
              RequestingTokenSource = None
              Requesting = false
              ButtonTitle = "Randomize"
              Message = err.ToString() }

  let runInView view = Mu.run init update view
