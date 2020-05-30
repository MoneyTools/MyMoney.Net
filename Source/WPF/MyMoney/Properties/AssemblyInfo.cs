using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("MyMoney")]
[assembly: AssemblyDescription("A personal money management application")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

//In order to begin building localizable applications, set 
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
    //(used if a resource is not found in the page, 
    // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
    //(used if a resource is not found in the page, 
    // app, or any theme specific resource dictionaries)
)]


// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: NeutralResourcesLanguageAttribute("")]

[assembly: InternalsVisibleTo(@"MyMoneyTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010075BA8B492C6DFC7D931414074F83ED4C2277EB06E3D2F0AC39A0EFDA3F4333436CC2E09BDCF70EEFD440487708441AA7B64B43D8147E1002B3D0754720C7E277214FFC3E9B9472FBC12BCAE89E9DABF7C13057B54DEC078896FB800C694907FAFAB201E8707CACC55A169D35D0CE9E1F47EA61AF75DD30BB362B22DCC1EBD7AF")]


// See ../Version/*.cs