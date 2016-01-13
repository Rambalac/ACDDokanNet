ACD Dokan.NET
=============

This is [Dokan.NET](https://github.com/dokan-dev/dokan-dotnet) based driver for Amazon Cloud Drive. 
In other words you can use Amazon Cloud Drive as real disk drive in Windows. 
Not just in Windows Explorer, but in any application.

![Sample](/images/Amazon Cloud Drive as real drive.png)

Login is done via default browser, so application itself can not get your login and password, 
and if you already logged into Amazon Cloud Drive you don't need to enter anything, just click a button.

Pros
----
* Amazon Cloud Drive is presented as real drive working not only in Windows Explorer but in many other apps (with issues). 
* Driver presents Amazon Cloud Drive as it is with folders and files. 
* Driver does not create any special files on your Amazon Cloud Drive.
* Same Amazon Cloud Drive can be used on multiple PCs with this Driver or in Web simultaneously (with issues). 


Issues
------
* Disk caching is done only for files with size less 20Mb. Big files are partially cached in memory and random access can be slow. Common video files are big and require random access to play. It's very unlikely you can play any video directly, but you can copy it to real drive first.
* Only new files can be written. To overwrite delete first.
* Files cannot be opened for Read and Write simultaneously except new files.
* Some applications can report some files cannot be opened. Still such files can be reopened later.
* Sometimes Explorer thumbnails get broken.
* There can be a conflict if you try to upload files with same name from different apps or web, only the first uploaded file will remain.

Notes
-----
* Copying file from Amazon Cloud Drive into different folder in the same cloud will download file and reupload it back.
* There is no limit for Upload cache folder where file copies for upload are stored.
* Folders are cached in memory for 60 seconds. If you deleted or uploaded some files in other way they will not appear/disappear same time even if you refresh folder, you have to wait up to 60 seconds and then refresh.
* Communication between Amazon Cloud Drive and this driver is secured by SSL, but there is no built-in way, at least for now, to encrypt files before uploading to Amazon Cloud Drive.

Prerequisites
-------------
- For use
    * Windows 7 or newer
    * [Dokany](https://github.com/dokan-dev/dokany/releases) is required.

- For developing
    * VS 2015
    * .NET 4.5

Installation
------------
* Read [Issues](#issues) section for limitations, it may not work for apps you want to use.
* Install Dokany with DokanInstall_x.x.x_redist.exe from https://github.com/dokan-dev/dokany/releases .
* Install my latest release from https://github.com/Rambalac/ACDDokanNet/releases/latest .
* Open Settings from App list.
* Select drive letter if needed.
* Mount
* Amazon Cloud Drive page should be opened
* Login and/or confirm to allow app to access Amazon Cloud Drive.
* If I did everything right, you should get drive letter with your Amazon Cloud Drive.

Tested apps
-----------
#### Windows Explorer
There can be issues with thumbnails, refresh to fix.

#### Lightroom 4
Keep catalog on real drive. 

Browsing and editing generally works. There can be some glitches with some files. Open file in develop mode, if you see an error switch to another file and after few seconds switch back, repeat if needed.

Catalog backup currently does not work due requirement for write already created files.

News
----
### 2016-01-13
* Release 1.2.0
* Implemented ReadWrite for files small enough to be downloaded in 30sec
* Added ReadOnly option

### 2016-01-06
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
