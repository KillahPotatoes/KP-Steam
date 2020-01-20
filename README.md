## KP-Steam

KP-Steam is a simple command line utility for Steam Workshop.

```
$ kpsteam -h
Usage: kpsteam [options] [command]

Options:
  -?|-h|--help  Show help information

Commands:
  download      Queues Steam Workshop item for download without subscribing the item.
  upload        Uploads file (legacy) or folder to Steam workshop.
```

```
$ kpsteam upload -h
Uploads file (legacy) or folder to Steam workshop.

Usage: kpsteam upload [options]

Options:
  -a|--app <APP_ID>                Steam AppId
  -i|--item <ITEM_ID>              Workshop Id of item to update
  -p|--path <PATH>                 Content path
  -l|--legacy[:<LEGACY>]           Legacy, single file based upload mode.
  -c|--changenotes <CHANGE_NOTES>  Change Notes (not supported in legacy mode)
  -?|-h|--help                     Show help information
```

```
$ kpsteam download -h
Queues Steam Workshop item for download without subscribing the item.

Usage: kpsteam download [options]

Options:
  -a|--app <APP_ID>    Steam AppId
  -i|--item <ITEM_ID>  Workshop Id of item to download
  -?|-h|--help         Show help information
```