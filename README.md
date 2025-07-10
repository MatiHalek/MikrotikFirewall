# MikrotikFirewall
MikrotikFirewall is a simple tool which allows you to **control access to websites of other devices on the same network as yours by configuring Mikrotik**.\
\
Simply enter your router login and password, then add the sites you want to allow on other devices to the list and let the program do the rest for you.\
At any time, you can revert changes or check the log, which will display the detailed history of operations and errors (if any occurred).
<p align="center">
<img width="269" height="432" alt="MikrotikFirewall app" src="https://github.com/user-attachments/assets/8029406b-0739-4cf5-af99-51cea6e124ff" />
</p>


MikrotikFirewall uses [plink](https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html) to perform operations. It will be downloaded automatically if not found on the drive from which the program is run. 
> [!IMPORTANT]
> You will need to run the program as an administrator if started from the system partition.

MikrotikFirewall runs on **64-bit versions of Windows 7 SP1, Windows 8.1, Windows 10 and Windows 11**. You may be prompted to download [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) before using this app.\
\
You can download this tool from Releases page or, if needed, easily change the commands the program executes by modifying the code (for exmaple, in Visual Studio 2022). Instructions are provided in the code as comments.\
\
MikrotikFirewall was created using Windows Presentation Foundation (WPF) and [WPF UI](https://wpfui.lepo.co/) library.
