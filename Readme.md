ACD Dokan.NET
=============

This is [Dokan.NET](https://github.com/dokan-dev/dokan-dotnet) based driver for Amazon Cloud Drive. 
In other words you can use Amazon Cloud Drive as real disk drive in Windows. 
Not just in Windows Explorer, but in any application.

![Sample](/images/Amazon Cloud Drive as real drive.png)

Login is done via default browser, so it does not need your login and password.

Prerequisites
-------------
- For use
    * Windows 7 or newer
    * [Dokany](https://github.com/dokan-dev/dokany/releases) is required.

- For developing
    * VS 2015
    * .NET 4.5

Warning
-------
As it's Beta version it may crush together with your OS and/or (really it should not, but...) damage files in your Amazon Cloud Drive. Use it on your one risk.

News
----
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