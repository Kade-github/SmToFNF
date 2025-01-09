# An FNF Converter for the latest version of the game!

Supported (and tested version of the game): **v0.5.3**

Metadata Version: **v2.2.4**

Chart Version: **v2.0.0**

# Support

- BPM Changes
- Mines/Fakes (will add custom note type, you must implement it yourself in game tho, sorry)
- Automatic camera focus
- Conversion of every important atribute in the file!

## How to use

### SM File setup

The file must contain a difficulty called, "Hard", "Normal", or "Easy." (normal in arrow vortex has to be hand written in the .sm file)

I did this because I dunno, I wanted to. fuck you.

Anyways it will only convert these diffs and skip the rest. <3 <3

### The thing

Go to the release tab, and grab the latest version.

Then drag a .sm file onto the exe and it'll pop out two files for ya!

```bash
SmToFnF.exe - Convert Stepmania files to Friday Night Funkin files
    "Usage: SmToFnf.exe <input file> [output file]"
```

## Compile

- C#
- [.NET 7.0/8.0]
- An IDE

First make sure you have the submodules,

```bash
git submodule update --init --recursive
```

Then build it.

Ya done.
