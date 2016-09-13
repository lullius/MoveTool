# MoveTool
A tool that outputs JSON files from Street Fighter 5's BCM, BAC and BCH (uasset) files and rebuilds those files from JSON.

## How to use
Drag Uasset or JSON files on top of MoveTool.exe

## Todo
Find out what all the values means and fix remaining BAC files. Se Bugs below.
Many values are probably read in the wrong format (short/int etc).

## Bugs
Unit test shows that MoveTool is not able to recreate these files:
  Not correct length: BAC_B59
  Bytes does not match up: BAC_CMN Position: 21C Original was: 0 Created was: 1C
  Not correct length: BAC_EFE
  
### Thanks
All the people who made the original Ono! stuff for SF4.
https://github.com/dantarion/ssf4ae-tools/
Looking over the source helped me understand the old format, and the new one is very similar.
I don't know everyone involved in that project or what they each did so I'll list the ones mentioned in the tool's About page in alphabetical order:

ACCELERATOR

ahfb

Anotak

Banana Ken

Bebopfan

combovid.com

Comeback Mechanic

Dandy J

Dantarion

Error1

Eternal

Gojira

hunterk

Illitirit

Mnszyk

Piecemontee

Polarity

Providenceangle

razer5070

Sindor

sonichurricane.com

SSJ George Bush

Waterine

yeb

Zandwich

Zeipher

zentax.com

I hope I got everyone and that I wrote your names correctly.


Dantarion's boxdox was also really helpful!

http://watissf.dantarion.com/sf5/boxdox3/

And the JSON

http://watissf.dantarion.com/sf5/out/json/


I would also like to thank Killbox and Venom44 for helping me find a lot of unknowns. Check out their mod (WIP) here:

http://steamcommunity.com/groups/SFVJUGGLEMOD

### Other stuff
This tool uses Json.NET from Newtonsoft which uses an MIT license:

The MIT License (MIT)

Copyright (c) 2007 James Newton-King

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
