using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Navigation;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.Core.Contracts.Services;

namespace XMCL2025.ViewModels
{
    /// <summary>
    /// 角色管理页面的ViewModel
    /// </summary>
    public partial class 角色管理ViewModel : ObservableRecipient, INavigationAware
    {
        private readonly IFileService _fileService;

        /// <summary>
        /// 当前角色信息
        /// </summary>
        [ObservableProperty]
        private MinecraftProfile _currentProfile;

        /// <summary>
        /// 新用户名（用于改名功能）
        /// </summary>
        [ObservableProperty]
        private string _newUsername = string.Empty;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="fileService">文件服务</param>
        public 角色管理ViewModel(IFileService fileService)
        {
            _fileService = fileService;
        }

        /// <summary>
        /// 导航到页面时调用
        /// </summary>
        /// <param name="parameter">导航参数</param>
        public void OnNavigatedTo(object parameter)
        {
            if (parameter is MinecraftProfile profile)
            {
                CurrentProfile = profile;
                NewUsername = profile.Name;
            }
        }

        /// <summary>
        /// 离开页面时调用
        /// </summary>
        public void OnNavigatedFrom()
        {
            // 页面导航离开时的清理逻辑
        }

        /// <summary>
        /// 保存用户名修改命令
        /// </summary>
        [RelayCommand]
        private void SaveUsername()
        {
            if (!string.IsNullOrWhiteSpace(NewUsername) && NewUsername != CurrentProfile.Name)
            {
                CurrentProfile.Name = NewUsername;
                // 保存修改到文件
                // 这里需要实现保存角色数据的逻辑
            }
        }

        /// <summary>
        /// 设置皮肤命令
        /// </summary>
        [RelayCommand]
        private void SetSkin()
        {
            // 实现设置皮肤逻辑
        }

        /// <summary>
        /// 设置披风命令
        /// </summary>
        [RelayCommand]
        private void SetCape()
        {
            // 实现设置披风逻辑
        }
    }
}