ACD Dokan.NET
=============
This is [Dokan.NET](https://github.com/dokan-dev/dokan-dotnet) based driver for Amazon Cloud Drive. 
In other words you can use Amazon Cloud Drive as real disk drive in Windows. 
Not just in Windows Explorer, but in any application.

![Sample](/images/Amazon Cloud Drive as real drive.png)

Login is done via default web browser, so application itself can not get your login and password, 
and if you already logged into Amazon Cloud Drive you don't need to enter anything, just click a button.

Info
----
Currently I'm making major update to version 2 which supports multiple cloud accounts and can be extended to support clouds other than Amazon Cloud. Just for example I implemented MS OneDrive. It will be released as soon as Dokan 2 release and I finish new type of caching that should eliminated most of file updating and big file reading issues.

Shell Extension
---------------
![Context Menu](/images/Context Menu.png)

Now you can get temp links in Windows Explorer for selected files or open temp link of one selected file in your app used for that file extension. Temp links will work for few days only, Amazon Cloud Drive does not provide any way to create permanent links to files.

Also you can open folders in browser on Amazon Cloud Drive web site.

Pros
----
* Amazon Cloud Drive is presented as real drive working not only in Windows Explorer but in many other apps (with issues). 
* Driver presents Amazon Cloud Drive as it is with folders and files. You will see the same content as on Amazon Cloud Drive web site. 
* Driver does not create any special files on your Amazon Cloud Drive.
* Same Amazon Cloud Drive can be used on multiple PCs with this Driver or in Web simultaneously (with issues). 


Issues
------
* Disk caching is done only for files with size less 20Mb. This can be changed, but be careful, Windows will try open all files you see in explorer. Big files are partially cached in memory and random access can be slow. Common video files are big and require random access to play. It's very unlikely you can play any video directly, but you can copy it to real drive first.
* Files cannot be opened for Read and Write if size is bigger than cached file size.
* Some applications can report some files cannot be opened. Still such files can be reopened later.
* Sometimes Explorer thumbnails get broken.
* There can be a conflict if you try to upload files with same name from different apps or web, only the first uploaded file will remain.

Issues reporting
----------------
If you did not get your cloud mounted or have other reason, sad to hear it. Here what you can do.
* First thing to try - check for the latest version
* If it does not help
  * Close app in System Tray
  * Run it again through "Run as Administrator"
  * Mount and try to repeat your problem.
  * Go to Options section
  * Click Export Log, select location and file name, Save
  * Click Open GitHub issue, follow GitHub instruction to create Issue and attach exported log.
 
* If you have problems preventing to open settings windows in "Run as Administrator"
  * Open Windows Event Viewer, go to Windows Logs - Application
  * Filter by Event Source ACDDokan.NET.
  * Check if events do no contain anything too private, events usually contain path to files, but there is no any account name or more over any password as application cannot get them. 
  * If there are some really private messages select all and unselect bad, Save Selected Events...
  * If nothing wrong Save Filtered Log File As...
  * Go to https://github.com/Rambalac/ACDDokanNet/issues
  * Create new issue and attach log file



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
    * Check [Build](Build.md) info page

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

Browsing and editing generally works. There can be some glitches with some files. Open file in develop mode, if you see an error then switch to another file and after few seconds switch back, repeat if needed.

Catalog backup currently does not work due requirement for write already created files.

News
----
### 2016-04-14
* Minor release 1.5.5. Fixed file names with special symbols.

### 2016-03-02
* Add Windows Explorer Shell Extension with additional functionality in Explorer context menu. 
Now you can get temp links or open them directly in your app default for that file extension
* Installer can autorun setting window.

### 2016-01-19
* Release 1.4.0
* Implemented access check. Only mounter and SYSTEM accounts can access drive.

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
