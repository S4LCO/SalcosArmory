using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SalcosArmory.Configurator.Models;
using SalcosArmory.Configurator.Services;

namespace SalcosArmory.Configurator;

public partial class MainWindow : Window
{
    private readonly ConfigRepository _repository = new();
    private ConfigWorkspace? _workspace;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var initialDirectory = ConfigLocator.FindInitialConfigDirectory(App.StartupArguments);
        if (initialDirectory is not null)
        {
            LoadWorkspace(initialDirectory);
            return;
        }

        SetStatus("Select your SPT folder or SalcosArmory mod folder to begin.");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_workspace is null || !HasUnsavedChanges())
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "There are unsaved configuration changes. Close without saving?",
            "SALCO's ARMORY Configurator",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        e.Cancel = answer != MessageBoxResult.Yes;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Select the SPT folder, SalcosArmory folder, or its config folder",
            Multiselect = false,
            InitialDirectory = _workspace?.ConfigDirectory ?? Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!ConfigLocator.TryResolve(dialog.FolderName, out var configDirectory))
        {
            ShowError(
                "No SALCO's ARMORY configuration was found in the selected folder. " +
                "Select the SPT installation, SPT_Runtime, SalcosArmory, or config folder.");
            return;
        }

        LoadWorkspace(configDirectory);
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null || !ConfirmDiscardChanges())
        {
            return;
        }

        LoadWorkspace(_workspace.ConfigDirectory);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveWorkspace(showSuccessMessage: true);
    }

    private void ResetTab_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        if (ConfigTabs.SelectedIndex > (int)ConfigSection.RuntimeInjection)
        {
            MessageBox.Show(
                this,
                "Additional compatibility and future config files do not have embedded defaults. " +
                "Use the automatic backups to restore them.",
                "No embedded default",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var section = (ConfigSection)ConfigTabs.SelectedIndex;
        var answer = MessageBox.Show(
            this,
            $"Restore the {(ConfigTabs.SelectedItem is TabItem tab ? tab.Header : "selected")} tab to SALCO's ARMORY defaults?\n\n" +
            "Nothing is written until you press Save changes.",
            "Restore defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _repository.ResetSection(_workspace, section);
            RefreshDataContext();
            SetStatus("Defaults loaded into the selected tab. Save to apply them.");
        }
        catch (Exception ex)
        {
            ShowException("Defaults could not be loaded.", ex);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        if (HasUnsavedChanges())
        {
            var answer = MessageBox.Show(
                this,
                "Save the current changes before exporting the profile?",
                "Export profile",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel
                || answer == MessageBoxResult.Yes && !SaveWorkspace(showSuccessMessage: false))
            {
                return;
            }
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export SALCO's ARMORY configuration profile",
            Filter = "SALCO's ARMORY profile (*.zip)|*.zip",
            FileName = $"SalcosArmory-profile-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            AddExtension = true,
            DefaultExt = ".zip"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            if (File.Exists(dialog.FileName))
            {
                File.Delete(dialog.FileName);
            }

            _repository.ExportProfile(dialog.FileName);
            SetStatus($"Profile exported to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowException("The configuration profile could not be exported.", ex);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null || !ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import SALCO's ARMORY configuration profile",
            Filter = "SALCO's ARMORY profile (*.zip)|*.zip",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Importing replaces every matching configuration file in the selected profile. " +
            "A complete backup will be created first. Continue?",
            "Import profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = _repository.ImportProfile(dialog.FileName, _workspace.ConfigDirectory);
            var configDirectory = _workspace.ConfigDirectory;
            LoadWorkspace(configDirectory);
            SetStatus($"Imported {result.ImportedFiles} file(s). Backup: {result.BackupPath}");
        }
        catch (Exception ex)
        {
            ShowException("The configuration profile could not be imported.", ex);
        }
    }

    private void AddWaylandCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        var row = new WaylandCategoryRow { Name = "NewCategory", LoyaltyLevel = 1, Stock = 3 };
        _workspace.Wayland.Categories.Add(row);
        WaylandCategoryGrid.SelectedItem = row;
        WaylandCategoryGrid.ScrollIntoView(row);
    }

    private void RemoveWaylandCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is not null && WaylandCategoryGrid.SelectedItem is WaylandCategoryRow row)
        {
            _workspace.Wayland.Categories.Remove(row);
        }
    }

    private void AddWaylandItem_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        var row = new WaylandItemRow { Enabled = true, LoyaltyLevel = 1, Stock = 1 };
        _workspace.Wayland.ItemOverrides.Add(row);
        WaylandItemGrid.SelectedItem = row;
        WaylandItemGrid.ScrollIntoView(row);
    }

    private void RemoveWaylandItem_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is not null && WaylandItemGrid.SelectedItem is WaylandItemRow row)
        {
            _workspace.Wayland.ItemOverrides.Remove(row);
        }
    }

    private void AddArmorClass_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        var row = new SoftArmorClassRow
        {
            BaseDurability = 70,
            BluntThroughput = 0.32,
            RepairCost = 100,
            FrontBackFleaPrice = 30_000,
            FrontBackHandbookPrice = 25_000,
            StaticLootWeight = 100,
            WaylandStock = 2
        };
        _workspace.SoftArmor.Classes.Add(row);
        ArmorClassGrid.SelectedItem = row;
        ArmorClassGrid.ScrollIntoView(row);
    }

    private void RemoveArmorClass_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is not null && ArmorClassGrid.SelectedItem is SoftArmorClassRow row)
        {
            _workspace.SoftArmor.Classes.Remove(row);
        }
    }

    private void AddArmorPosition_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        var row = new SoftArmorPositionRow();
        _workspace.SoftArmor.Positions.Add(row);
        ArmorPositionGrid.SelectedItem = row;
        ArmorPositionGrid.ScrollIntoView(row);
    }

    private void RemoveArmorPosition_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is not null && ArmorPositionGrid.SelectedItem is SoftArmorPositionRow row)
        {
            _workspace.SoftArmor.Positions.Remove(row);
        }
    }

    private void AddRuntimeTarget_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
        {
            return;
        }

        var row = new RuntimeTargetRow();
        _workspace.RuntimeInjection.Targets.Add(row);
        RuntimeTargetsGrid.SelectedItem = row;
        RuntimeTargetsGrid.ScrollIntoView(row);
    }

    private void RemoveRuntimeTarget_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is not null && RuntimeTargetsGrid.SelectedItem is RuntimeTargetRow row)
        {
            _workspace.RuntimeInjection.Targets.Remove(row);
        }
    }

    private void AddRuntimeSlot_Click(object sender, RoutedEventArgs e)
    {
        if (RuntimeTargetsGrid.SelectedItem is not RuntimeTargetRow target)
        {
            ShowError("Select a runtime injection target before adding a slot.");
            return;
        }

        var row = new RuntimeSlotRow();
        target.Slots.Add(row);
        RuntimeSlotsGrid.SelectedItem = row;
        RuntimeSlotsGrid.ScrollIntoView(row);
    }

    private void RemoveRuntimeSlot_Click(object sender, RoutedEventArgs e)
    {
        if (RuntimeTargetsGrid.SelectedItem is RuntimeTargetRow target
            && RuntimeSlotsGrid.SelectedItem is RuntimeSlotRow row)
        {
            target.Slots.Remove(row);
        }
    }

    private void RuntimeTargetsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RuntimeSlotsGrid.Items.Refresh();
    }

    private void LoadWorkspace(string configDirectory)
    {
        try
        {
            _workspace = _repository.Load(configDirectory);
            ConfigLocator.Remember(configDirectory);
            RefreshDataContext();
            EditorRoot.IsEnabled = true;
            SetStatus($"Loaded configuration from {configDirectory}");
        }
        catch (Exception ex)
        {
            _workspace = null;
            DataContext = null;
            EditorRoot.IsEnabled = false;
            ShowException("SALCO's ARMORY configuration could not be loaded.", ex);
        }
    }

    private bool SaveWorkspace(bool showSuccessMessage)
    {
        if (_workspace is null)
        {
            return false;
        }

        CommitPendingEdits();
        if (HasBindingErrors(this))
        {
            ShowError("At least one field contains an invalid number. Correct the red-marked fields before saving.");
            return false;
        }

        var validation = ConfigValidator.Validate(_workspace);
        if (!validation.IsValid)
        {
            ShowError("The configuration contains invalid values:\n\n" + FormatMessages(validation.Errors));
            return false;
        }

        if (validation.Warnings.Count > 0)
        {
            var answer = MessageBox.Show(
                this,
                "The configuration has warnings:\n\n" + FormatMessages(validation.Warnings) + "\n\nSave anyway?",
                "Configuration warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return false;
            }
        }

        if (IsSptRunning())
        {
            var answer = MessageBox.Show(
                this,
                "SPT or Escape from Tarkov is currently running. The new settings will only be applied after a restart.\n\nSave anyway?",
                "SPT is running",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return false;
            }
        }

        try
        {
            var result = _repository.Save(_workspace);
            var message = result.ChangedFiles == 0
                ? "No configuration changes to save."
                : $"Saved {result.ChangedFiles} file(s). Backup: {result.BackupPath}";
            SetStatus(message);

            if (showSuccessMessage && result.ChangedFiles > 0)
            {
                MessageBox.Show(
                    this,
                    "Configuration saved successfully. Restart the SPT server to apply the changes.",
                    "SALCO's ARMORY Configurator",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowException("The configuration could not be saved.", ex);
            return false;
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (_workspace is null || !HasUnsavedChanges())
        {
            return true;
        }

        return MessageBox.Show(
            this,
            "Discard the unsaved configuration changes?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private bool HasUnsavedChanges()
    {
        if (_workspace is null)
        {
            return false;
        }

        try
        {
            CommitPendingEdits();
            return _repository.HasChanges(_workspace);
        }
        catch
        {
            return true;
        }
    }

    private void RefreshDataContext()
    {
        DataContext = null;
        DataContext = _workspace;
        if (_workspace?.RuntimeInjection.Targets.Count > 0)
        {
            RuntimeTargetsGrid.SelectedIndex = 0;
        }

        if (_workspace?.AdvancedFiles.Count > 0)
        {
            AdvancedFilesList.SelectedIndex = 0;
        }
    }

    private void CommitPendingEdits()
    {
        Keyboard.ClearFocus();
        foreach (var grid in new[]
                 {
                     WaylandCategoryGrid,
                     WaylandItemGrid,
                     ArmorClassGrid,
                     ArmorPositionGrid,
                     RuntimeTargetsGrid,
                     RuntimeSlotsGrid
                 })
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }

    private static bool HasBindingErrors(DependencyObject root)
    {
        if (Validation.GetHasError(root))
        {
            return true;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (HasBindingErrors(VisualTreeHelper.GetChild(root, index)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSptRunning()
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes.Any(process =>
                process.ProcessName.Equals("SPT.Server", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Equals("EscapeFromTarkov", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string FormatMessages(IEnumerable<string> messages)
    {
        var items = messages.Take(15).Select(message => $"• {message}").ToArray();
        return string.Join(Environment.NewLine, items)
            + (messages.Skip(15).Any() ? Environment.NewLine + "• …" : string.Empty);
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "MutedTextBrush");
    }

    private void ShowError(string message)
    {
        SetStatus(message, isError: true);
        MessageBox.Show(
            this,
            message,
            "SALCO's ARMORY Configurator",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void ShowException(string message, Exception exception)
    {
        ShowError($"{message}\n\n{exception.Message}");
    }
}
