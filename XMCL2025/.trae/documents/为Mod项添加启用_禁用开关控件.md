# 为Mod项添加启用/禁用开关控件

## 实现概述
为每个mod项添加一个开关控件，用于控制mod的启用状态，实现状态同步、即时反馈和持久化存储。

## 核心实现步骤

### 1. 重构ModInfo类
- 修改ModInfo继承自ObservableObject，支持属性变更通知
- 使用[ObservableProperty]特性标记可观察属性
- 实现计算属性Name，根据IsEnabled状态动态显示/隐藏.Disabled后缀
- 保持现有文件命名逻辑，通过文件名判断启用状态

### 2. 添加Mod启用状态切换命令
- 在版本管理ViewModel中添加RelayCommand用于处理开关切换
- 实现文件重命名逻辑，添加/移除.disabled后缀
- 更新ModInfo对象的属性，触发UI更新
- 添加错误处理机制，处理文件操作异常

### 3. 更新Mod列表项模板
- 在XAML中为ModInfo DataTemplate添加ToggleSwitch控件
- 调整Grid.ColumnDefinitions，将开关放置在名称和删除按钮之间
- 绑定ToggleSwitch的IsOn属性到ModInfo.IsEnabled
- 绑定ToggleSwitch的Command属性到ViewModel的切换命令
- 配置开关的OnContent和OffContent，保持界面一致性

### 4. 确保持久化存储
- 利用现有文件命名机制实现状态持久化
- 加载mod列表时，根据文件名自动设置IsEnabled状态
- 确保开关状态与文件实际状态保持同步

## 技术要点

1. **MVVM模式**：使用CommunityToolkit.Mvvm的ObservableProperty和RelayCommand
2. **属性变更通知**：确保ModInfo属性变化能即时反映到UI
3. **文件操作**：使用FileService或直接文件操作重命名mod文件
4. **UI设计**：保持ToggleSwitch与现有界面风格一致
5. **错误处理**：添加异常捕获，确保操作失败时不崩溃

## 实现效果
- 开关状态与mod启用状态实时同步
- 启用时显示原始名称，禁用时添加.Disabled后缀
- 状态切换即时生效，无需确认
- 开关设计符合现有界面风格
- 状态持久化存储，重启后保持不变