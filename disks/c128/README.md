# Commodore 128 CP/M 3.0 disks

The CP/M Plus (3.0) system and utility disks Commodore shipped with the C128, as D64 images for a 1541. They are
the images published at https://www.zimmers.net/anonftp/pub/cbm/demodisks/c128/ and are included here so the C128
emulation can boot CP/M out of the box (they are also served by the web front-end). The copyright holders permit
redistribution of these disks for personal use; the original site's notice applies: they were distributed with new
Commodore hardware and may be downloaded for own use, copying and selling them is not permitted.

| File | Content |
|---|---|
| `cpm.system.622-580745.d64` | CP/M Plus 3.0 system disk, 1985 ("CP/M 3.0 On the Commodore 128 8 DEC 85"). Boots on a 1541. |
| `cpm.system.622-3282252.d64` | CP/M system disk dated May 28, 1987 (the 1571/1581-era release, also boots from a 1541). |
| `cpm.utilities.d64` | CP/M Plus 3.0 utilities, 1985 (PIP, ED, SUBMIT, FORMAT, ...). |
| `cpm.utilities3.d64` | Utilities from the May 28, 1987 system disk. |

Booting: attach a system disk to the C128 (device 8) and reset; the KERNAL finds the boot sector, loads
`CPM+.SYS` through the drive and hands the machine to the Z80. With a real 1541 this takes about two minutes of
emulated time; use warp mode. The test `C128SystemTests.Boots_CPM_3_From_The_System_Disk` (`Category=Slow`)
does exactly that and waits for the `A>` prompt on the 80 column screen.
