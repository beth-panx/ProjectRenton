using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using Microsoft.Windows.Sdk;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ProjectRenton
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("USER32.DLL")]
        private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            //var process = Process.GetProcessesByName("Teams"); //.FirstOrDefault(w => w.MainWindowTitle?.Contains("Teams") == true);

            //PInvoke.EnumWindows((hwnd, bla) =>
            //{
            //    return true;
            //}, new LPARAM(0));
            //if (process != null)
            //{
            //    PInvoke.SetForegroundWindow((HWND)process.MainWindowHandle);
            //}

            var windows = GetOpenWindows().Where(w => w.Value.Contains("Teams"));

            list.ItemsSource = windows; //.Select(w => w.Value);

        }

        private static IDictionary<HWND, string> GetOpenWindows()
        {
            HWND shellWindow = PInvoke.GetShellWindow();
            Dictionary<HWND, string> windows = new Dictionary<HWND, string>();

            PInvoke.EnumWindows((hWnd, lParam) =>
            {
                if (hWnd.Equals(shellWindow)) return true;
                if (!PInvoke.IsWindowVisible(hWnd)) return true;

                int length = PInvoke.GetWindowTextLength(hWnd);
                if (length == 0) return true;


                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                windows[hWnd] = builder.ToString();
                return true;

            }, new LPARAM(0));

            return windows;
        }

        private async void handButton_Click(object sender, RoutedEventArgs e)
        {
            if (list.SelectedItem != null)
            {
                var window = (KeyValuePair<HWND, string>)list.SelectedItem;
                PInvoke.SetForegroundWindow((HWND)window.Key);

                var inputs = new INPUT[6];

                // https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes

                inputs[0].type = INPUT_typeFlags.INPUT_KEYBOARD;
                inputs[0].Anonymous.ki.wVk = 0x11; //CTRL

                inputs[1].type = INPUT_typeFlags.INPUT_KEYBOARD;
                inputs[1].Anonymous.ki.wVk = 0x10; //SHIFT

                inputs[2].type = INPUT_typeFlags.INPUT_KEYBOARD;
                inputs[2].Anonymous.ki.wVk = 0x4B; //K

                inputs[3].type = INPUT_typeFlags.INPUT_KEYBOARD;
                inputs[3].Anonymous.ki.wVk = 0x11; //CTRL
                inputs[3].Anonymous.ki.dwFlags = keybd_eventFlags.KEYEVENTF_KEYUP;

                inputs[4].type = INPUT_typeFlags.INPUT_KEYBOARD;
                inputs[4].Anonymous.ki.wVk = 0x10; //SHIFT
                inputs[4].Anonymous.ki.dwFlags = keybd_eventFlags.KEYEVENTF_KEYUP;

                inputs[5].type = INPUT_typeFlags.INPUT_KEYBOARD;
                inputs[5].Anonymous.ki.wVk = 0x4B; //K
                inputs[5].Anonymous.ki.dwFlags = keybd_eventFlags.KEYEVENTF_KEYUP;

                // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
                PInvoke.SendInput(inputs, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        private async void ButtonRun_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.FileTypeFilter.Add(".jpg");
            fileOpenPicker.FileTypeFilter.Add(".png");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;
            var hwnd = this.As<IWindowNative>().WindowHandle;

            //Make folder Picker work in Win32

            var initializeWithWindow = fileOpenPicker.As<IInitializeWithWindow>();
            initializeWithWindow.Initialize(hwnd);
            fileOpenPicker.FileTypeFilter.Add("*");

            StorageFile selectedStorageFile = await fileOpenPicker.PickSingleFileAsync();

            SoftwareBitmap softwareBitmap;
           
            //UIPreviewImage.Source = imageSource;

            //TODO: Make this model agnostic??

            //Use Squeezenet model to classify image
            var list = await HandRaiserModel.DetectObjects(selectedStorageFile);

        }

        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        internal interface IWindowNative
        {
            IntPtr WindowHandle { get; }
        }
    }
}
