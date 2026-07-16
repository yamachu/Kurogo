# Third-Party Notices

## OSCmooth

The AAP (Animated Animator Parameter) feedback-loop technique used to implement
`Drive.Smoothing` in `KurogoAnimatorGenerator.BuildSmoothingFeedback` was designed
after reading the source of **OSCmooth**, specifically
`OSCmoothAnimationHandler.CreateParameterSmoothingBlendTree`:

- Repository: https://github.com/regzo2/OSCmooth
- Author: Mitchell Taylor

The algorithm (three Animator Float parameters and three Simple1D blend trees
forming a "remember the previous frame's value and blend it with the raw input"
feedback loop, using -1/+1 anchor clips to linearly reconstruct a parameter's
value through an Animated Animator Parameter) is reimplemented from scratch
against the Animator As Code (AAC) API rather than copied verbatim, but the
design is directly derived from that project. Its license is reproduced below
in full, as good practice regardless of whether verbatim reproduction of source
occurred.

```
MIT License

Copyright (c) 2022 Mitchell Taylor

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
