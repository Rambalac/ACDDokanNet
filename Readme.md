ACD Dokan.NET
=============
This is [Dokan.NET](https://github.com/dokan-dev/dokan-dotnet) based driver for Amazon Cloud Drive. 
In other words you can use Amazon Cloud Drive as real disk drive in Windows. 
Not just in Windows Explorer, but in any application.

Starting from version 1.6.0 multiple clouds are supported, as example I added Microsoft OneDrive. As it's not ACD only I'm looking for better name. CloudHold?

![Sample](/images/Amazon Cloud Drive as real drive.png)

###Amazon CloudDrive
Login is done via default web browser, so application itself can not get your login and password, 
and if you already logged into Amazon Cloud Drive you don't need to enter anything, just click a button.

Shell Extension
---------------
###Links
![Context Menu](/images/Context Menu.png)

You can get temp links in Windows Explorer for selected files or open temp link of one selected file in your app used for that file extension. Temp links will work for few days only, Amazon Cloud Drive does not provide any way to create permanent links to files.

Also you can open folders in browser on Amazon Cloud Drive web site.

###Upload here
![Upload Here](/images/Uploadhere.png)

You can copy files in Explorer and with *Upload here* in destination cloud folder context menu (right mouse button) start upload files and folders instantly.

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
* There can be different problems if you try to mess with uploading files like rename or move.

Notes
-----
* All files copied to cloud drive get to temp folder first and only then driver starts uploading. Because driver does not see what you are doing in Explorer it cannot now where it was copied from and has to get solid copy for upload. There is workaround with *Upload here* in context menu
* Copying file from Amazon Cloud Drive into different folder in the same cloud will download file and reupload it back.
* There is no limit for Upload cache folder where file copies for upload are stored.
* Folders are cached in memory for 60 seconds. If you deleted or uploaded some files in other way like in web they will not appear/disappear same time even if you refresh folder, you have to wait up to 60 seconds and then refresh.
* Communication between Amazon Cloud Drive and this driver is secured by SSL, but there is no built-in way, at least for now, to encrypt files before uploading to Amazon Cloud Drive.

Prerequisites
-------------
- For use
    * Windows 7 or newer
    * [Dokany 1.0.0](https://github.com/dokan-dev/dokany/releases/tag/v0.8.0) is required.

- For developing
    * VS 2015
    * .NET 4.5
    * Check [Build](Build.md) info page

Installation
------------
* Read [Issues](#issues) and [Notes](#notes) sections tobe sure you know about main limitations, it may not work for apps you want to use.

* Install Dokany
  * For versions after 1.6.0 install Dokany with DokanSetup-1.0.0.5000.exe from https://github.com/dokan-dev/dokany/releases/tag/v1.0.0 .
  * For versions before 1.6.0 install DokanInstall_0.8.0_redist.exe from https://github.com/dokan-dev/dokany/releases/tag/v0.8.0 .

* Install my latest release from https://github.com/Rambalac/ACDDokanNet/releases/latest .
* Open Settings from App list.
* Add cloud.
* Select drive letter if needed and rename disk (renaming in explorer will work only till next mounting).
* Mount.
* Amazon Cloud Drive page should be opened (Or other login screen for other clouds).
* Login and/or confirm to allow app to access cloud.
* If I did everything right, you should get drive letter with your cloud.

Tested apps
-----------
#### Windows Explorer
There can be issues with thumbnails, refresh to fix.

#### Lightroom 4
Keep catalog on real drive. 

Browsing and editing generally works. There can be some glitches with some files. Open file in develop mode, if you see an error then switch to another file and after few seconds switch back, repeat if needed.

Catalog backup currently does not work due requirement for write already created files.

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
  * Check if events do not contain anything too private, events usually contain path to files, but there is no any account name or more over any password as application cannot get them. 
  * If there are some really private messages select all and unselect bad, Save Selected Events...
  * If nothing wrong Save Filtered Log File As...
  * Go to https://github.com/Rambalac/ACDDokanNet/issues
  * Create new issue and attach log file

News
----
### 2016-11-05
* Release 1.6.2.
* New feature! "**Upload here**" from cloud folder context menu in Explorer will upload files copied into clipboard **without** buffering that files in temp folder. Upload starts immediately. Folders are supported.
* Fixed some operations with uploading files like cancel in the list and cancel Explorer copy.
* Some tuning upload errors processing.
* Added automatic update check but need to be tested better. Still update should be run manually.
* Dokany got update to 1.0.1 use it. https://github.com/dokan-dev/dokany/releases/tag/v1.0.1

### 2016-10-11
* Fixed v1.6.0 glitches with Dokany
* Fixed some UI issues like cloud delete

### 2016-10-09
Removed Prerelease 1.6.0 because of Windows Explorer locking out and only hardware Reset button helps. Looks like this happened after updating to Dokany 1.0.0, but need more testing.

### 2016-10-09
* Prerelease 1.6.0.
* Now you can mount multiple cloud services. As example and test Microsoft OneDrive was added. Theoretically it should support any libraries as Cloud.*.dll as Addons.
* Uploading files list is displayed to show file name, progress and if failed error. Upload can be cancelled.

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
