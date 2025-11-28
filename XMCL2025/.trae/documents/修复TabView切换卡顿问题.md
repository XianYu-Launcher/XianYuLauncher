### 问题分析
1. **FilteredVersions属性效率低下**：每次访问都会创建新的ObservableCollection，触发大量UI更新和垃圾回收
2. **TabView内容预渲染**：可能会渲染所有标签内容，包括未选中标签的大量数据
3. **缺少UI虚拟化优化**：ListView可能未启用高效的虚拟化

### 解决方案

#### 1. 优化FilteredVersions属性
- 将计算属性改为普通属性，使用ObservableCollection存储过滤结果
- 在SearchText变化时更新过滤结果，避免重复创建集合
- 示例代码：
  ```csharp
  // 过滤后的版本列表
  [ObservableProperty]
  private ObservableCollection<Core.Contracts.Services.VersionEntry> _filteredVersions = new();
  
  // 监听SearchText变化，更新过滤结果
  partial void OnSearchTextChanged(string value)
  {
      UpdateFilteredVersions();
  }
  
  private void UpdateFilteredVersions()
  {
      FilteredVersions.Clear();
      IEnumerable<Core.Contracts.Services.VersionEntry> filtered = Versions;
      
      if (!string.IsNullOrWhiteSpace(SearchText))
      {
          filtered = Versions.Where(v => v.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
      }
      
      foreach (var version in filtered)
      {
          FilteredVersions.Add(version);
      }
  }
  ```

#### 2. 实现TabView延迟加载
- 监听TabView.SelectionChanged事件
- 仅在TabViewItem被选中时加载数据
- 为每个TabViewItem添加Loaded事件处理
- 示例代码：
  ```xaml
  <TabView x:Name="ResourceTabView" SelectionChanged="ResourceTabView_SelectionChanged">
      <TabViewItem Loaded="VersionTab_Loaded">
          <!-- 版本下载内容 -->
      </TabViewItem>
  </TabView>
  ```

#### 3. 优化ListView虚拟化
- 确保ListView启用高效的UI虚拟化
- 设置VirtualizingStackPanel.IsVirtualizing="True"
- 设置VirtualizingStackPanel.VirtualizationMode="Recycling"
- 示例代码：
  ```xaml
  <ListView>
      <ListView.ItemsPanel>
          <ItemsPanelTemplate>
              <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling"/>
          </ItemsPanelTemplate>
      </ListView.ItemsPanel>
  </ListView>
  ```

#### 4. 使用异步数据加载
- 确保数据加载操作异步执行
- 避免阻塞UI线程
- 示例代码：
  ```csharp
  [RelayCommand]
  private async Task LoadVersionsAsync()
  {
      IsVersionLoading = true;
      try
      {
          var manifest = await _minecraftVersionService.GetVersionManifestAsync();
          var versionList = manifest.Versions.ToList();
          
          // 更新最新版本信息
          LatestReleaseVersion = versionList.FirstOrDefault(v => v.Type == "release")?.Id ?? string.Empty;
          LatestSnapshotVersion = versionList.FirstOrDefault(v => v.Type == "snapshot")?.Id ?? string.Empty;
          
          // 更新版本列表
          Versions.Clear();
          foreach (var version in versionList)
          {
              Versions.Add(version);
          }
          
          // 更新过滤后的版本列表
          UpdateFilteredVersions();
      }
      finally
      {
          IsVersionLoading = false;
      }
  }
  ```

### 预期效果
- TabView切换延迟从500ms减少到100ms以内
- 版本列表加载更流畅
- 内存使用更高效
- 整体UI响应性提升

### 实现步骤
1. 优化FilteredVersions属性实现
2. 添加TabView.SelectionChanged事件处理
3. 优化ListView虚拟化配置
4. 确保所有数据加载操作异步执行
5. 测试TabView切换性能

### 注意事项
- 保持现有功能不变
- 确保向后兼容
- 避免引入新的性能问题
- 测试不同场景下的表现