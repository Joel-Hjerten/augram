# Third-party notice: StrokesPlus classic recognizer

The code in this folder (`StrokeResampler`, `AngleSequence`, `SampleScorer`, `GestureMatcher`) is a port of the gesture-match math and `GetGestureName` from StrokesPlus classic (`StrokesPlusHook/StrokesPlusHook.cpp`, https://github.com/minyoad/StrokesPlus), which StrokesPlus itself credits as ported to C++ from HighSign by Dylan Vester (http://highsign.codeplex.com/). The port is deliberately faithful: resampling by arc length, index-by-index comparison of segment headings, the `|mean delta × 100/π − 100|` score, and the original's division of the delta sum by the precision P although only P−1 deltas exist (kept as `ScoringMode.Legacy`, the default, because the threshold of 75 and users' trained templates were tuned against it). Rotation sensitivity is a feature of the algorithm and is preserved.

------------------------------------------------------
StrokesPlus Source License:
------------------------------------------------------

The MIT License (MIT)

Copyright (c) 2014 Rob Larkin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

------------------------------------------------------
Other Licenses and Credits (from the StrokesPlus LICENSE.md):
------------------------------------------------------

Gesture recognition code ported to C++ from the following project:
(full credit to Dylan Vester; a great guy and coding genius!)
http://highsign.codeplex.com/
License: http://highsign.codeplex.com/license
