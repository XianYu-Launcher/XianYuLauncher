using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.ViewModels;

namespace XMCL2025.Views
{
    /// <summary>
    /// 角色管理页面
    /// </summary>
    public sealed partial class 角色管理Page : Page
    {
        /// <summary>
        /// ViewModel实例
        /// </summary>
        public 角色管理ViewModel ViewModel
        {
            get;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public 角色管理Page()
        {
            ViewModel = App.GetService<角色管理ViewModel>();
            InitializeComponent();
        }

        /// <summary>
        /// 导航到页面时调用
        /// </summary>
        /// <param name="e">导航事件参数</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // 将导航参数传递给ViewModel
            if (ViewModel is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }
        }

        /// <summary>
        /// 离开页面时调用
        /// </summary>
        /// <param name="e">导航取消事件参数</param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            
            // 通知ViewModel离开页面
            if (ViewModel is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }
        }
    }
}