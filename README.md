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

// Update
type Event = Reset | ...

let update model event =
  match event with
  | Reset -> Update { model with ... }
  ...

// View
type ViewController (handle:IntPtr) =
  inherit UIViewController (handle)
  ...

  override x.ViewDidLoad () =
    base.ViewDidLoad ()
    // Glue model update view
    Mu.run initModel update x

  interface Mu.IView<Model, Event> with
    member x.BindModel model binder =
      binder.Bind <@ model.Field @> (fun fieldValue -> x.someLabel.Text <- fieldValue)
      ...

    member x.BindEvent emit =
      x.resetButton.TouchUpInside.Add (fun _ -> emit Reset)
      ...
```

That is really the essence of The Elm Architecture!

With **Mu**, Model and Update are separated from view, this means that they can shared across platforms, implementing `Mu.IView<'Model, 'Event>` interfaces is the only required step to support specific platform.

### Model

Model should be a DTO(Data Transfer Object) only, immutable record is the best way to represent model in F#.

### Update

```fsharp
type Update<'model, 'event> =
  | Update of 'model
  | UpdateWithSideEffects of 'model * SideEffects<'model, 'event>
  | SideEffects of SideEffects<'model, 'event>
and SideEffects<'model, 'event> =
  'model -> EmitEvent<'event> -> unit
and EmitEvent<'event> =
  'event -> unit
```

Update is about changing model through event: `'model -> 'event -> Update<'model, 'event>`.

Not all updates are just model changes, side effects without or with model changing are common. **Mu** provides current model state and the event emitter when performing side effects.

### View

```fsharp
type IView<'model, 'event> =
  abstract BindModel: 'model -> IBinder -> unit
  abstract BindEvent: EmitEvent<'event> -> unit
and IBinder =
  abstract Bind: Expr<'value> -> ('value -> unit) -> unit
```

View is only an interface in **Mu**, this is the most unobtrusive way to introduce 3rd lib into your project. `BindModel` provides model and binder to sync model states to view elements, and `BindEvent` provides an event emitter to make user input possible.

### Run

A **Mu** component is simply a record contained model initialization, model updater and view:

```fsharp
type T<'model, 'event> =
  { init: unit -> 'model
    update: 'model -> 'event -> Update<'model, 'event>
    view: IView<'model, 'event> }
```

Run component on view when it's ready with:

```fsharp
Mu.run' component
// or
Mu.run init update view
```

## Examples

👉 [Examples](Examples)

## Usage

- Install from NuGet: [https://www.nuget.org/packages/com.realazy.Mu](https://www.nuget.org/packages/com.realazy.Mu)
- Add this project to your solution, or directly add `Mu.fs`. (Yes `Mu` is a single file project, less than 100 LOC! 🤯)

## LICENSE

MIT

## Author

- Blog: [realazy.com](https://realazy.com) (Chinese)
- Github: [@cxa](https://github.com/cxa)
- Twitter: [@\_cxa](https://twitter.com/_cxa) (Chinese mainly)
