# Undecoded-gap analysis (operator box)

Mirfiles pack A (~7.2G). Text/metrics only — no bake-out or Data in git.

| Count | Value |
| --- | --- |
| ImagesListed | 2143132 |
| ImagesDecoded | 1869869 |
| ImagesBlank | 273263 |
| listed − decoded | 273263 (= Blank exactly) |
| ParseFailures | 0 |
| Packed / decoded | 1869867 / 1869869 |

**Dual metrics**

1. Non-blank image decode: **100%** (1869869 / (2143132 − 273263))
2. Listed-slot decode: **87.25%** (1869869 / 2143132) — includes empty Mir library slots

The 12.75% listed-slot gap is blanks in the denominator, not a decoder bug. Do not invent pixels for blanks. No decoder fix required for that gap.

Two frames decoded-not-packed is an optional packer edge. Catalog 162/248 remains missing-on-disk only (86 slots listed, not synthesized).
