using Siemens.Engineering;
using Siemens.Engineering.AddIn.Menu;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Communication;
using Siemens.Engineering.Hmi.Cycle;
using Siemens.Engineering.Hmi.Globalization;
using Siemens.Engineering.Hmi.RuntimeScripting;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.Hmi.TextGraphicList;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace TIA_Add_In_ToolPlus
{
    public class AddIn : ContextMenuAddIn
    {
        private readonly TiaPortal _tiaPortal;

        public AddIn(TiaPortal tiaPortal) : base("ToolPlus")
        {
            _tiaPortal = tiaPortal;
        }

        protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRootSubmenu)
        {
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("导出", Export_OnClick);
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("导入", Import_OnClick);
        }

        //导出块、用户数据类型、变量表：
        //轮询选中的项目树中的块、用户数据类型、变量表，根据选中类型执行不同的导出xml方法
        //弹出选择文件夹窗体，获取文件夹路径
        //导出的xml路径为文件夹路径+选中项的名称+.xml
        private void Export_OnClick(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            try
            {
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                if (folderBrowserDialog.ShowDialog(new Form()
                { TopMost = true, WindowState = FormWindowState.Maximized }) == DialogResult.OK)
                {
                    foreach (IEngineeringObject iEngineeringObject in menuSelectionProvider.GetSelection())
                    {
                        //查询名称是否包含“/”，如果包含替换更“_”
                        string name = iEngineeringObject.GetAttribute("Name").ToString();
                        while (name.Contains("/"))
                        {
                            name = name.Replace("/", "_");
                        }

                        //导出数据
                        string filePath = Path.Combine(folderBrowserDialog.SelectedPath, name + ".xml");
                        Export(iEngineeringObject, filePath);
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //导入块、用户数据类型、变量表：
        //弹出选择文件窗体，后缀限制为xml
        //递归到"程序块"文件夹或用户自定义文件夹或PLC变量文件夹，再执行导入到文件夹
        private void Import_OnClick(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            try
            {
                //选择并打开文件
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "xml File(*.xml)| *.xml"
                };
                if (openFileDialog.ShowDialog(new Form()
                { TopMost = true, WindowState = FormWindowState.Maximized }) == DialogResult.OK
                    && !string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    foreach (PlcBlockGroup plcBlockGroup in menuSelectionProvider.GetSelection())
                    {
                        foreach (string fileName in openFileDialog.FileNames)
                        {
                            Import(plcBlockGroup, fileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出PLC数据
        /// </summary>
        /// <param name="exportItem"></param>
        /// <param name="exportPath"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="EngineeringException"></exception>
        /// <returns></returns>
        public static string Export(IEngineeringObject exportItem, string exportPath)
        {
            // plcBlock.Export(fileInfo,ExportOptions.WithDefaults);
            if (exportItem == null)
                throw new ArgumentNullException(nameof(exportItem), "Parameter is null");
            if (string.IsNullOrEmpty(exportPath))
                throw new ArgumentException("Parameter is null or empty", nameof(exportPath));

            // string filePath = Path.GetFullPath(exportPath);
            const ExportOptions exportOption = ExportOptions.WithDefaults;

            switch (exportItem)
            {
                case PlcBlock item:
                    {
                        if (item.ProgrammingLanguage == ProgrammingLanguage.ProDiag ||
                            item.ProgrammingLanguage == ProgrammingLanguage.ProDiag_OB)
                            return null;
                        if (item.IsConsistent)
                        {
                            // filePath = Path.Combine(filePath, AdjustNames.AdjustFileName(GetObjectName(item)) + ".xml");
                            if (File.Exists(exportPath))
                            {
                                File.Delete(exportPath);
                            }

                            item.Export(new FileInfo(exportPath), exportOption);

                            return exportPath;
                        }

                        throw new EngineeringException(string.Format(CultureInfo.InvariantCulture,
                            "Block: {0} is inconsistent! Export will be aborted!", item.Name));
                    }
                case PlcTagTable _:
                case PlcType _:
                case ScreenOverview _:
                case ScreenGlobalElements _:
                case Siemens.Engineering.Hmi.Screen.Screen _:
                case TagTable _:
                case Connection _:
                case GraphicList _:
                case TextList _:
                case Cycle _:
                case MultiLingualGraphic _:
                case ScreenTemplate _:
                case VBScript _:
                case ScreenPopup _:
                case ScreenSlidein _:
                    {
                        // Directory.CreateDirectory(filePath);
                        // filePath = Path.Combine(filePath, AdjustNames.AdjustFileName(GetObjectName(exportItem)) + ".xml");
                        // File.Delete(filePath);
                        if (File.Exists(exportPath))
                        {
                            File.Delete(exportPath);
                        }

                        var parameter = new Dictionary<Type, object>
                    {
                        { typeof(FileInfo), new FileInfo(exportPath) },
                        { typeof(ExportOptions), exportOption }
                    };
                        exportItem.Invoke("Export", parameter);
                        return exportPath;
                    }
                case PlcExternalSource _:
                    //Directory.CreateDirectory(filePath);
                    //filePath = Path.Combine(filePath, AdjustNames.AdjustFileName(GetObjectName(exportItem)));
                    //File.Delete(filePath);
                    //File.Create(filePath);
                    //return filePath;
                    break;
            }

            return null;
        }

        /// <summary>
        /// 导入PLC数据
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="filePath"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static void Import(IEngineeringCompositionOrObject destination, string filePath)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination), "Parameter is null");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Parameter is null or empty", nameof(filePath));

            FileInfo fileInfo = new FileInfo(filePath);
            const ImportOptions importOption = ImportOptions.Override;
            filePath = fileInfo.FullName;


            switch (destination)
            {
                case CycleComposition _:
                case ConnectionComposition _:
                case MultiLingualGraphicComposition _:
                case GraphicListComposition _:
                case TextListComposition _:
                    {
                        var parameter = new Dictionary<Type, object>();
                        parameter.Add(typeof(string), filePath);
                        parameter.Add(typeof(ImportOptions), importOption);
                        // Export prüfen
                        (destination as IEngineeringComposition).Invoke("Import", parameter);
                        break;
                    }
                case PlcBlockGroup group when Path.GetExtension(filePath).Equals(".xml"):
                    group.Blocks.Import(fileInfo, importOption);
                    break;
                case PlcBlockGroup _:
                    {
                        IEngineeringObject currentDestination = destination as IEngineeringObject;
                        while (!(currentDestination is PlcSoftware))
                        {
                            currentDestination = currentDestination.Parent;
                        }

                        PlcExternalSourceComposition col = (currentDestination as PlcSoftware).ExternalSourceGroup
                            .ExternalSources;

                        string sourceName = Path.GetRandomFileName();
                        sourceName = Path.ChangeExtension(sourceName, ".src");
                        PlcExternalSource src = col.CreateFromFile(sourceName, filePath);
                        src.GenerateBlocksFromSource();
                        src.Delete();
                        break;
                    }
                case PlcTagTableGroup group:
                    group.TagTables.Import(fileInfo, importOption);
                    break;
                case PlcTypeGroup group:
                    group.Types.Import(fileInfo, importOption);
                    break;
                case PlcExternalSourceGroup group:
                    {
                        PlcExternalSource temp = group.ExternalSources.Find(Path.GetFileName(filePath));
                        if (temp != null)
                            temp.Delete();
                        group.ExternalSources.CreateFromFile(Path.GetFileName(filePath), filePath);
                        break;
                    }
                case TagFolder folder:
                    folder.TagTables.Import(fileInfo, importOption);
                    break;
                case ScreenFolder folder:
                    folder.Screens.Import(fileInfo, importOption);
                    break;
                case ScreenTemplateFolder folder:
                    folder.ScreenTemplates.Import(fileInfo, importOption);
                    break;
                case ScreenPopupFolder folder:
                    folder.ScreenPopups.Import(fileInfo, importOption);
                    break;
                case ScreenSlideinSystemFolder folder:
                    folder.ScreenSlideins.Import(fileInfo, importOption);
                    break;
                case VBScriptFolder folder:
                    folder.VBScripts.Import(fileInfo, importOption);
                    break;
                case ScreenGlobalElements _:
                    (destination.Parent as HmiTarget)?.ImportScreenGlobalElements(fileInfo, importOption);
                    break;
                case ScreenOverview _:
                    (destination.Parent as HmiTarget)?.ImportScreenOverview(fileInfo, importOption);
                    break;
            }
        }

    }
}
