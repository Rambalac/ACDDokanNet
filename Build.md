Building
========

AmazonSecret.cs
---------------
To connect to Amazon CLoud Drive you need to register your application and get app secret keys.
Create AmazonSecret.cs in ACD.DokanNet.Gui project and replace values with your app secrets 
```C#
namespace Azi.ACDDokanNet.Gui
{
    public static class AmazonSecret
    {
        public static string clientId = "amzn1.application-oa2-client.xxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        public static string clientSecret = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    }
}
```
