ACD Dokan.NET
=============

This is [Dokan.NET](https://github.com/dokan-dev/dokan-dotnet) based driver for Amazon Cloud Drive. 
In other words you can use Amazon Cloud Drive as real disk drive in Windows. 
Not just in Windows Explorer, but in any application.

![Sample](/images/Amazon Cloud Drive as real drive.png)

Login is done via default browser, so application itself can not get your login and password, 
and if you already logged into Amazon Cloud Drive you dont need to enter anything, just click a button.

Pros
----
* Driver presents Amazon Cloud Drive as it is with folders and files. 
* Driver does not create any special files on your Amazon Cloud Drive.
* Same Amazon Cloud Drive can be used on multiple PCs with this Driver or in Web simultaneously. 
There can be a conflict if you try to upload file which was also upload same time with other way, only first file will remain.

Issues
------
* Only new files can be written. To overwrite delete first.
* Files cannot be opened for Read and Write simultaneously.
* Some applications can report some file cannot be opened. Still such files can be reopened later.
* Sometimes Explorer thumbnails get broken.

Notes
-----
* Copying file from Amazon Cloud Drive into different folder in the same cloud will download file and reupload it back.
* There is no limit for Upload folder where files for upload are stored.
* Folders are cached in memory for 60 seconds. If you deleted or 

Prerequisites
-------------
- For use
    * Windows 7 or newer
    * [Dokany](https://github.com/dokan-dev/dokany/releases) is required.

- For developing
    * VS 2015
    * .NET 4.5

News
----
### 2015-01-06
* Release 1.1.0
* Fixed crash on exit in Windows 7
* Upload will be resumed on next start if app was closed before all files got uploaded.
* Improved performance for file reading.

### 2015-12-28
* First release!
* GUI for settings
* Many fixes
* Limit for file size in disk cache and total cache size. Other files cached only in memory.

### 2015-12-20
* New files upload - done
* Files read cached - done
* Create folder - done
* File/folders move/rename - done

Still there are issues. 
* Moving files from Amazon Cloud Drive can show security dialogs about files from bad place. 
* Explorer does not accept names for renaming longer 8 symbols.

### 2015-12-14
Now it can read files. But it's still not cached and reading from apps, like photos, can be very very slow. But linear reading like copying is possible.

### 2015-12-11
Can browse folders!