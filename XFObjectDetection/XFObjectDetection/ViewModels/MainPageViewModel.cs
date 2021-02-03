﻿using Acr.UserDialogs;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Plugin.Media.Abstractions;
using Plugin.Permissions.Abstractions;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using XFObjectDetection.Common;
using XFObjectDetection.Providers;

namespace XFObjectDetection.ViewModels
{
    public class MainPageViewModel : ViewModelBase
    {
        private readonly IMedia _mediaPlugin;
        private readonly IPermissions _permissionsPlugin;
        private readonly IUserDialogs _userDialogs;
        public MainPageViewModel(INavigationService navigationService, IMedia mediaPlugin, IPermissions permissionsPlugin, IUserDialogs userDialogs)
            : base(navigationService)
        {
            _mediaPlugin = mediaPlugin;
            _permissionsPlugin = permissionsPlugin;
            _userDialogs = userDialogs;

            this.TakePictureCommand = new DelegateCommand(async () => await OnTakePictureAsync());
            this.PickPictureCommand = new DelegateCommand(async () => await OnPickPictureAsync());
        }

        private ImageSource _imageSource;
        public ImageSource ImageSource
        {
            get => _imageSource;
            set
            {
                SetProperty(ref _imageSource, value);
                this.IsImagePlaceholderVisible = value == null;
            }
        }

        private string _resultText = string.Empty;
        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        private bool _isImagePlaceholderVisible = true;
        public bool IsImagePlaceholderVisible
        {
            get => _isImagePlaceholderVisible;
            set => SetProperty(ref _isImagePlaceholderVisible, value);
        }

        public DelegateCommand TakePictureCommand { get; private set; }
        public DelegateCommand PickPictureCommand { get; private set; }

        private async Task OnTakePictureAsync()
        {
            var cameraStatus = await _permissionsPlugin.CheckPermissionStatusAsync(Permission.Camera);
            var storageStatus = await _permissionsPlugin.CheckPermissionStatusAsync(Permission.Storage);

            if (cameraStatus != PermissionStatus.Granted || storageStatus != PermissionStatus.Granted)
            {
                var statusDict = await _permissionsPlugin.RequestPermissionsAsync(new Permission[] { Permission.Camera, Permission.Storage });
                cameraStatus = statusDict[Permission.Camera];
                storageStatus = statusDict[Permission.Storage];
            }

            if (cameraStatus == PermissionStatus.Granted && storageStatus == PermissionStatus.Granted)
            {
                var file = await _mediaPlugin.TakePhotoAsync(new StoreCameraMediaOptions
                {
                    PhotoSize = PhotoSize.Medium
                });

                if (file != null)
                {
                    _userDialogs.ShowLoading();

                    this.ImageSource = ImageSource.FromStream(() =>
                    {
                        var stream = file.GetStream();
                        return stream;
                    });

                    await AnalyzeImage(file);

                    _userDialogs.HideLoading();
                }

            }
            else
            {
                await _userDialogs.AlertAsync("Unable to take photos", "Permissions Denied", "Ok");
            }
        }

        private async Task OnPickPictureAsync()
        {

            var cameraStatus = await _permissionsPlugin.CheckPermissionStatusAsync(Permission.Camera);
            var storageStatus = await _permissionsPlugin.CheckPermissionStatusAsync(Permission.Storage);

            if (cameraStatus != PermissionStatus.Granted || storageStatus != PermissionStatus.Granted)
            {
                var statusDict = await _permissionsPlugin.RequestPermissionsAsync(new Permission[] { Permission.Camera, Permission.Storage });
                cameraStatus = statusDict[Permission.Camera];
                storageStatus = statusDict[Permission.Storage];
            }

            if (cameraStatus == PermissionStatus.Granted && storageStatus == PermissionStatus.Granted)
            {
                var file = await _mediaPlugin.PickPhotoAsync(new Plugin.Media.Abstractions.PickMediaOptions
                {
                    PhotoSize = PhotoSize.Medium
                });


                if (file != null)
                {
                    _userDialogs.ShowLoading();

                    this.ImageSource = ImageSource.FromStream(() =>
                    {
                        var stream = file.GetStream();
                        return stream;
                    });

                    await AnalyzeImage(file);

                    _userDialogs.HideLoading();
                }
            }
            else
            {
                await _userDialogs.AlertAsync("Unable to pick photos", "Permissions Denied", "Ok");
            }


        }

        public async Task AnalyzeImage(MediaFile file)
        {
            var imageAnalysis = await MakeAnalysisRequest(Constants.ComputerVisionUriBase, Constants.ComputerVisionSubscriptionKey1, file.GetStream());

            if (imageAnalysis != null)
            {


                var strBuilder = new StringBuilder();
                foreach (var obj in imageAnalysis.Objects)
                {
                    strBuilder.AppendLine(string.Join(Environment.NewLine, obj.ObjectProperty) + " Confidence: " + (obj.Confidence * 100).ToString());
                }
                strBuilder.AppendLine(string.Empty);
                ResultText = strBuilder.ToString();
            }
            else
            {
                ResultText = string.Empty;
            }
        }

        public async Task<DetectResult> MakeAnalysisRequest(string uriBase, string subscriptionKey, Stream imageStream)
        {
            try
            {
                var computerVision = new ComputerVisionClient(
                    new ApiKeyServiceClientCredentials(subscriptionKey),
                    new System.Net.Http.DelegatingHandler[] { });

                computerVision.Endpoint = "https://mphilaivision.cognitiveservices.azure.com";

                var analysis = await computerVision.DetectObjectsInStreamAsync(imageStream);

                return analysis;
            }
            catch (Exception ex)
            {
                await _userDialogs.AlertAsync(ex.Message);
            }

            return null;

        }
    }
}
