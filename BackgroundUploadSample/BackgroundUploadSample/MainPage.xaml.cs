using Microsoft.Toolkit.Services.OneDrive;
using Microsoft.Toolkit.Services.Services.MicrosoftGraph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace BackgroundUploadSample
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private OneDriveStorageFolder oneDriveAppFolder = null;

        public MainPage()
        {
            this.InitializeComponent();

            SignIn();
        }

        private async Task SignIn()
        {
            try
            {
                string[] scopes = new string[] { MicrosoftGraphScope.FilesReadWriteAppFolder };
                OneDriveService.Instance.Initialize("6f7f6556-bf94-4cdb-9834-ce192d9fa5ae", scopes, null, null);
                if (await OneDriveService.Instance.LoginAsync())
                {
                    oneDriveAppFolder = await OneDriveService.Instance.AppRootFolderAsync();
                }
                else
                {
                    throw new Exception("Unable to sign in");
                }
            }
            catch { }
        }

        private async void SelectAndUpload(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                //Background Upload
                var upload = await CreateBackgroundUploadForItemAsync(oneDriveAppFolder, file);
                upload.Priority = BackgroundTransferPriority.High;
                Progress<UploadOperation> progressCallback = new Progress<UploadOperation>(UploadProgress);
                //CancellationToken token = default(CancellationToken);
                CancellationTokenSource cts = new CancellationTokenSource();
                await upload.StartAsync().AsTask(cts.Token, progressCallback);
                //ResponseInformation response = upload.GetResponseInformation();
            }
        }

        private void UploadProgress(UploadOperation upload)
        {
            progress.Value = (int)((upload.Progress.BytesSent / (double)upload.Progress.TotalBytesToSend) * 100);
        }

        private async Task<UploadOperation> CreateBackgroundUploadForItemAsync(OneDriveStorageFolder destinationFolder, StorageFile sourceFile, BackgroundTransferCompletionGroup completionGroup = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (destinationFolder == null)
            {
                throw new ArgumentNullException(nameof(destinationFolder));
            }

            if (sourceFile == null)
            {
                throw new ArgumentNullException(nameof(sourceFile));
            }

            //var fileCreateNew = await destinationFolder.CreateFileAsync(desiredName, CreationCollisionOption.OpenIfExists);
            return await Task.Run(
                async () =>
                {
                    var requestMessage = OneDriveService.Instance.Provider.GraphProvider.Drive.Items[destinationFolder.OneDriveItem.Id].Content.Request().GetHttpRequestMessage();
                    await OneDriveService.Instance.Provider.GraphProvider.AuthenticationProvider.AuthenticateRequestAsync(requestMessage).AsAsyncAction().AsTask(cancellationToken);
                    var uploader = completionGroup == null ? new BackgroundUploader() : new BackgroundUploader(completionGroup);
                    foreach (var item in requestMessage.Headers)
                    {
                        uploader.SetRequestHeader(item.Key, item.Value.First());
                    }
                    uploader.SetRequestHeader("Filename", sourceFile.Name);
                    return uploader.CreateUpload(requestMessage.RequestUri, sourceFile);
                }, cancellationToken);
        }

    }
}
