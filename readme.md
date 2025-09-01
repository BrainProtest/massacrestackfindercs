# Massacre Stack Finder

Tries to select good massacre stack finders via a cobbled together heuristic. 

Takes into account:
- Number of potentially mission generating factions within reach
- Military Economy
- Number of potential alternative targets

Needs a file downloaded and passed to command execution, see [Input](./Input)

## Example Usage

```sh
./MassacreStackFinderCs <Path/Of/systemPopulated.json> [options]
```

### Options

```
MassacreStackFinderCs 1.0.0
Copyright (C) 2025 MassacreStackFinderCs

  -c, --cachefile     A file to use for a cache

  -o, --outputFile    Output file

  -s, --system        If defined, also yield a file for the system (assuming it passed initial selection)

  -n, --numResults    How many systems to return

  --help              Display this help screen.

  --version           Display version information.

  value pos. 0        Required. EDSM dump systemsPopulated.json
```

## What does it do?

