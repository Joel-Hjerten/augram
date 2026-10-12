# ScreenHost

Renders a `ScreenDeclaration` (ADR-0002 §5c): `ScreenHost` asks `ScreenRendererRegistry` for the `IScreenRenderer` of the declaration's type (form, list, text, component; found by scan). A new screen shape is a new renderer here.
