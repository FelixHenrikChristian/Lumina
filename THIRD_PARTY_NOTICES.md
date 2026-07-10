# Third-Party Notices

Lumina includes or distributes software from the projects below. Their licenses
remain in effect for their respective components.

## MIT-licensed components

- **Electron** — Copyright (c) Electron contributors; Copyright (c) 2013-2020
  GitHub Inc. — <https://github.com/electron/electron>
- **React and React DOM** — Copyright (c) Meta Platforms, Inc. and affiliates. —
  <https://github.com/facebook/react>
- **Zustand** — Copyright (c) 2019 Paul Henschel —
  <https://github.com/pmndrs/zustand>
- **liquid-glass-react** (adapted source in `src/vendor`) — Copyright 2025 MAX
  ROVENSKY — <https://github.com/rdev/liquid-glass-react>

The following MIT License text applies to each component listed above:

> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

## Chromium and bundled notices

Electron bundles Chromium and related third-party components. electron-builder
places Electron's `LICENSE.electron.txt` and Chromium's `LICENSES.chromium.html`
in the unpacked application. Those files contain the complete notices applicable
to the bundled runtime.

Development-only dependencies are listed in `package-lock.json`; their license
metadata and source distributions remain the authoritative notices for tools that
are not shipped as part of the Lumina application.
