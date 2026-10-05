# Threading

The thread table for the engine (hook thread, engine worker, tick timer, health poll, log drain) and what each may and may not do lives in [src/Augram.Engine/README.md](../../src/Augram.Engine/README.md), next to the code that enforces it. Rules in one line: the hook thread only translates, decides suppression from a shadow, and enqueues; the engine worker is the only caller of the capture state machine and the only injector of input; nothing in Engine touches the UI thread.
