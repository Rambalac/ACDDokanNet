Building
========

AmazonSecret.cs
---------------
To connect to Amazon CLoud Drive you need to register your application and get app secret keys.
Create AmazonSecret.cs in ACD.DokanNet.Gui project and replace values with your app secrets 
```C#
namespace Azi.Cloud.DokanNet.Gui
{
    public static class AmazonSecret
    {
        public static string clientId = "amzn1.application-oa2-client.xxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        public static string clientSecret = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    }
}
```

This file is already ignored on commit, just in case.

Strong Names
------------

All projects are signed. It's required for Windows Explorer Extension. You still can run the main Gui app if you remove signing from Project Properties and remove Strong Namer Nuget addon.