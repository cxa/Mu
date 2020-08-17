# Mu

Apply The Elm Architecture pattern on Xamarin stack, with another simple, unobtrusive way.

[![Build Status](https://travis-ci.org/cxa/Mu.svg?branch=master)](https://travis-ci.org/cxa/Mu)

## The Elm Architecture (TEA)

[TEA](https://guide.elm-lang.org/architecture/) is all about Model, Update, and View, in **Mu**, it's specific to:

- Model — the state of a GUI component
- Update — a way to update your state
- View — a way to view your state as `UIView(Controller)`, `NSView(Controller)`, `Activity` and other platform view stacks that Xamarin supported

This is a simple pattern for architecting GUI components.

```fsharp
// Model
type Model = { ... }

// Msgs, all update through sending message to perform
type Msg = Reset | ...

// Update
let update model msg =
  match msg with
  | Reset -> newModel, cmd
  ...

// View
type ViewController (handle:IntPtr) =
  inherit UIViewController (handle)
  ...

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    // Glue model update, and view
    Mu.run init update x

  interface Mu.IView<Model, Msg> with
    // Setup view and model relationship
    // We are using quotations for reactivity,
    // that means view will update automatically if model updated
    member x.BindModel model =
      <@
        x.someLabel.Text <- model.Field
        ...
      @>

    // Controls use `send` to send message to inform updates
    member x.BindMsg send =
      x.resetButton.TouchUpInside.Add (fun _ -> send Reset)
      ...
```

That is really the essence of The Elm Architecture!

With **Mu**, Model and Update are separated from view, this means that they can shared across platforms, implementing `Mu.IView<'model, 'msg>` interfaces is the only required step to support specific platform.

### Model

A model should be a DTO(Data Transfer Object) only, An immutable record is the best way to represent a model in F#.

### Update

The `Update` is about changing model through msg: `'model -> 'msg -> 'model, Cmd<'msg>`. Beside changing model, you can guide **`Mu`** to perform further actions via `Cmd<'msg>`.

### View

```fsharp
type Send<'msg> = 'msg -> unit

type IView<'model, 'msg> =
  abstract BindModel: 'model -> Expr<unit>
  abstract BindMsg: Send<'msg> -> unit
```

A `View` is only an interface in **`Mu`**, this is the most unobtrusive way to introduce 3rd lib into your project. Setup binding in `BindModel` to sync model to view elements, and `BindMsg` provides a message sender to make obtaining user input possible.

### Run

A **Mu** component is simply a record contained model initialization, model updater and view:

```fsharp
type T<'model, 'msg> =
    { Init: unit -> 'model
      Update: 'model -> 'msg -> 'model, Cmd<'msg>
      View: IView<'model, 'msg> }
```

Run component on view when it's ready with:

```fsharp
Mu.run' component
// or
Mu.run init update view
```

## Examples

👉 [Examples](Examples) contain some advance usages of async effects.

## Usage

- Install from NuGet: [https://www.nuget.org/packages/com.realazy.Mu](https://www.nuget.org/packages/com.realazy.Mu)
- Add this project to your solution, or directly add `Mu.fs`. (Yes `Mu` is a single file project, less than 200 SLOC! 🤯)

## LICENSE

MIT

## Author

- Blog: [realazy.com](https://realazy.com) (Chinese)
- Github: [@cxa](https://github.com/cxa)
- Twitter: [@\_cxa](https://twitter.com/_cxa) (Chinese mainly)
