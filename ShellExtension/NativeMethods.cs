// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace Azi.ShellExtension
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class NativeMethods
    {
        private enum AssocF
        {
            ASSOCF_NONE = 0x00000000,
            ASSOCF_INIT_NOREMAPCLSID = 0x00000001,
            ASSOCF_INIT_BYEXENAME = 0x00000002,
            ASSOCF_OPEN_BYEXENAME = 0x00000002,
            ASSOCF_INIT_DEFAULTTOSTAR = 0x00000004,
            ASSOCF_INIT_DEFAULTTOFOLDER = 0x00000008,
            ASSOCF_NOUSERSETTINGS = 0x00000010,
            ASSOCF_NOTRUNCATE = 0x00000020,
            ASSOCF_VERIFY = 0x00000040,
            ASSOCF_REMAPRUNDLL = 0x00000080,
            ASSOCF_NOFIXUPS = 0x00000100,
            ASSOCF_IGNOREBASECLASS = 0x00000200,
            ASSOCF_INIT_IGNOREUNKNOWN = 0x00000400,
            ASSOCF_INIT_FIXED_PROGID = 0x00000800,
            ASSOCF_IS_PROTOCOL = 0x00001000,
            ASSOCF_INIT_FOR_FILE = 0x00002000
        }

        private enum AssocStr
        {
            ASSOCSTR_COMMAND = 1,
            ASSOCSTR_EXECUTABLE = 2,
            ASSOCSTR_FRIENDLYDOCNAME = 3,
            ASSOCSTR_FRIENDLYAPPNAME = 4,
            ASSOCSTR_NOOPEN = 5,
            ASSOCSTR_SHELLNEWVALUE = 6,
            ASSOCSTR_DDECOMMAND = 7,
            ASSOCSTR_DDEIFEXEC = 8,
            ASSOCSTR_DDEAPPLICATION = 9,
            ASSOCSTR_DDETOPIC = 10,
            ASSOCSTR_INFOTIP = 11,
            ASSOCSTR_QUICKTIP = 12,
            ASSOCSTR_TILEINFO = 13,
            ASSOCSTR_CONTENTTYPE = 14,
            ASSOCSTR_DEFAULTICON = 15,
            ASSOCSTR_SHELLEXTENSION = 16,
            ASSOCSTR_DROPTARGET = 17,
            ASSOCSTR_DELEGATEEXECUTE = 18,
            ASSOCSTR_SUPPORTED_URI_PROTOCOLS = 19,
            ASSOCSTR_PROGID = 20,
            ASSOCSTR_APPID = 21,
            ASSOCSTR_APPPUBLISHER = 22,
            ASSOCSTR_APPICONREFERENCE = 23,
            ASSOCSTR_MAX = 24
        }

        public static string AssocQueryString(string extension)
        {
            const int S_OK = 0;
            const int S_FALSE = 1;

            uint length = 0;
            var ret = AssocQueryString(AssocF.ASSOCF_NONE, AssocStr.ASSOCSTR_COMMAND, extension, null, null, ref length);
            if (ret != S_FALSE)
            {
                throw new InvalidOperationException("Could not determine associated string");
            }

            var sb = new StringBuilder((int)length); // (length-1) will probably work too as the marshaller adds null termination
            ret = AssocQueryString(AssocF.ASSOCF_NONE, AssocStr.ASSOCSTR_COMMAND, extension, null, sb, ref length);
            if (ret != S_OK)
            {
                throw new InvalidOperationException("Could not determine associated string");
            }

            return sb.ToString();
        }

        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);
    }
}