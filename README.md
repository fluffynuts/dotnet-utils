# dotnet utils

## toggle-fusion

### what?

A simple batch script inspired by an answer on [StackOverflow](https://stackoverflow.com/questions/255669/how-to-enable-assembly-bind-failure-logging-fusion-in-net#answer-1527249)
to enable or disable Fusion logging so you can get to the bottom of pesky assembly errors

### where?
`bin/toggle-fusion.cmd`

### why?

Because
- I've read elsewhere that I needed only one of these flags (didn't work)
- I'm not going to remember all these flags
- I wasn't aware (before) that I would need to restart processes

### usage
- run with `--help` to see options
- run with no arguments for interactive mode

### batch scripts... ew!
Yes, I know, but I wanted minimal dependencies


## asmdeps

### what?

A command-line utility to list the dependencies of one or more assemblies. This code
has been hanging around in dropbox for ages -- it's time it was set free.


### why?
Because sometimes it's useful to have a little util to tell you an assembly
dependency chain. I've found myself looking for this over and over, and finally
found it again. So here it is.

### where?
a net452-capable binary can be found at:
```
bin/asmdeps.exe
```
source is in the `asmdeps` folder. You can build a new win32 executable with
`dotnet publish asmdeps/asmdeps.csproj`. This hasn't been tested on netcore,
but I guess it would work for netcore stuff?

### usage
`asmdeps.exe some.assembly.dll another.assembly.dll ...`

### code is ew
probably -- but it's functional