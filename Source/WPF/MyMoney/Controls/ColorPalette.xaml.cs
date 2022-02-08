using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Utilities;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for ColorPalette.xaml
    /// </summary>
    public partial class ColorPalette : UserControl, IView, IClipboardClient
    {
        public ColorPalette()
        {
            InitializeComponent();
        }

        #region IView
        public MyMoney Money { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        public string Caption => "ColorPalette";

        public object SelectedRow { get; set; }
        public ViewState ViewState { get; set; }
        public string QuickFilter { get; set; }
        public bool IsQueryPanelDisplayed { get; set; }


        public event EventHandler BeforeViewStateChanged;
        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        public void ActivateView()
        {
            if (PaletteList.ItemsSource == null)
            {
                List<ColorItem> items = new List<ColorItem>();
                foreach (var name in new string[] { 
                    "SystemAccentColor:    SystemControlBackgroundAccentBrush",
                    "SystemAccentColor:    SystemControlDisabledAccentBrush",
                    "SystemAccentColor:    SystemControlRevealFocusVisualBrush",
                    "SystemAccentColor:    SystemControlForegroundAccentBrush",
                    "SystemAccentColor:    SystemControlHighlightAccentBrush",
                    "SystemAccentColor:    SystemControlHighlightAltAccentBrush",
                    "SystemAccentColor:    SystemControlHighlightAltListAccentHighBrush",
                    "SystemAccentColor:    SystemControlHighlightAltListAccentLowBrush",
                    "SystemAccentColor:    SystemControlHighlightAltListAccentMediumBrush",
                    "SystemAccentColor:    SystemControlHighlightListAccentHighBrush",
                    "SystemAccentColor:    SystemControlHighlightListAccentLowBrush",
                    "SystemAccentColor:    SystemControlHighlightListAccentMediumBrush",
                    "SystemAccentColor:    SystemControlHighlightListAccentVeryHighBrush",
                    "SystemAccentColor:    SystemControlHighlightListAccentMediumLowBrush",
                    "SystemAccentColor:    SystemControlHyperlinkTextBrush",
                    "SystemAccentColor:    SystemControlHighlightAccentRevealBackgroundBrush",
                    "SystemAccentColor:    SystemControlHighlightAccent3RevealBackgroundBrush",
                    "SystemAccentColor:    SystemControlHighlightAccent2RevealBackgroundBrush",
                    "SystemAccentColor:    SystemControlHighlightAccent3RevealAccent2BackgroundBrush",
                    "SystemAccentColor:    SystemControlBackgroundAccentRevealBorderBrush",
                    "SystemAccentColor:    SystemControlHighlightAccentRevealBorderBrush",
                    "SystemAccentColor:    DataGridRowSelectedBackground",
                    "SystemAccentColor:    DataGridRowSelectedHoveredBackground",
                    "SystemAccentColor:    DataGridRowSelectedHoveredUnfocusedBackground",
                    "SystemAccentColor:    DataGridRowSelectedUnfocusedBackground",
                    "SystemAccentColorDark1:    SystemAccentColorDark1Brush",
                    "SystemAccentColorDark2:    SystemAccentColorDark2Brush",
                    "SystemAccentColorDark3:    SystemAccentColorDark3Brush",
                    "SystemAccentColorLight1:    SystemAccentColorLight1Brush",
                    "SystemAccentColorLight2:    SystemAccentColorLight2Brush",
                    "SystemAccentColorLight3:    SystemAccentColorLight3Brush",
                    "SystemAltHighColor:    SystemControlBackgroundAltHighBrush",
                    "SystemAltHighColor:    SystemControlForegroundAltHighBrush",
                    "SystemAltHighColor:    SystemControlHighlightAltAltHighBrush",
                    "SystemAltHighColor:    SystemControlPageBackgroundAltHighBrush",
                    "SystemAltHighColor:    SystemControlBackgroundAltHighRevealBackgroundBrush",
                    "SystemAltHighColor:    DataGridColumnHeaderBackgroundBrush",
                    "SystemAltHighColor:    PersonPictureForegroundThemeBrush",
                    "SystemAltMediumColor:    SystemControlBackgroundAltMediumBrush",
                    "SystemAltMediumColor:    SystemControlFocusVisualSecondaryBrush",
                    "SystemAltMediumColor:    SystemControlPageBackgroundAltMediumBrush",
                    "SystemAltMediumColor:    SystemControlPageBackgroundMediumAltMediumBrush",
                    "SystemAltMediumHighColor:    SystemControlBackgroundAltMediumHighBrush",
                    "SystemAltMediumHighColor:    SystemControlForegroundAltMediumHighBrush",
                    "SystemAltMediumHighColor:    SystemControlHighlightAltAltMediumHighBrush",
                    "SystemAltMediumLowColor:    SystemControlBackgroundAltMediumLowBrush",
                    "SystemBaseHighColor:    SystemControlBackgroundBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlDisabledBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlFocusVisualPrimaryBrush",
                    "SystemBaseHighColor:    SystemControlForegroundBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlHighlightAltBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlHighlightBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlHyperlinkBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlPageTextBaseHighBrush",
                    "SystemBaseHighColor:    SystemControlBackgroundBaseHighRevealBorderBrush",
                    "SystemBaseHighColor:    SystemControlHighlightBaseHighRevealBorderBrush",
                    "SystemBaseHighColor:    ButtonBackgroundPointerOver",
                    "SystemBaseHighColor:    PersonPictureEllipseBadgeForegroundThemeBrush",
                    "SystemBaseHighColor:    RepeatButtonBackgroundPointerOver",
                    "SystemBaseHighColor:    SplitButtonBackgroundPointerOver",
                    "SystemBaseHighColor:    ToggleButtonBackgroundPointerOver",
                    "SystemBaseLowColor:    SystemControlBackgroundBaseLowBrush",
                    "SystemBaseLowColor:    SystemControlDisabledBaseLowBrush",
                    "SystemBaseLowColor:    SystemControlForegroundBaseLowBrush",
                    "SystemBaseLowColor:    SystemControlHighlightAltBaseLowBrush",
                    "SystemBaseLowColor:    SystemControlHighlightBaseLowBrush",
                    "SystemBaseLowColor:    SystemControlPageBackgroundBaseLowBrush",
                    "SystemBaseLowColor:    SystemControlBackgroundBaseLowRevealBackgroundBrush",
                    "SystemBaseLowColor:    SystemControlBackgroundBaseLowRevealBorderBrush",
                    "SystemBaseLowColor:    SystemControlHighlightBaseLowRevealBorderBrush",
                    "SystemBaseMediumColor:    SystemControlBackgroundBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlForegroundBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlHighlightAltBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlHighlightBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlHyperlinkBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlPageBackgroundBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlPageTextBaseMediumBrush",
                    "SystemBaseMediumColor:    SystemControlBackgroundBaseMediumRevealBorderBrush",
                    "SystemBaseMediumColor:    SystemControlHighlightBaseMediumRevealBorderBrush",
                    "SystemBaseMediumColor:    PersonPictureEllipseFillThemeBrush",
                    "SystemBaseMediumHighColor:    SystemControlBackgroundBaseMediumHighBrush",
                    "SystemBaseMediumHighColor:    SystemControlForegroundBaseMediumHighBrush",
                    "SystemBaseMediumHighColor:    SystemControlHighlightAltBaseMediumHighBrush",
                    "SystemBaseMediumHighColor:    SystemControlHighlightBaseMediumHighBrush",
                    "SystemBaseMediumHighColor:    SystemControlHyperlinkBaseMediumHighBrush",
                    "SystemBaseMediumHighColor:    SystemControlBackgroundBaseMediumHighRevealBorderBrush",
                    "SystemBaseMediumHighColor:    SystemControlHighlightBaseMediumHighRevealBorderBrush",
                    "SystemBaseMediumLowColor:    SystemControlBackgroundBaseMediumLowBrush",
                    "SystemBaseMediumLowColor:    SystemControlDisabledBaseMediumLowBrush",
                    "SystemBaseMediumLowColor:    SystemControlForegroundBaseMediumLowBrush",
                    "SystemBaseMediumLowColor:    SystemControlHighlightAltBaseMediumLowBrush",
                    "SystemBaseMediumLowColor:    SystemControlHighlightBaseMediumLowBrush",
                    "SystemBaseMediumLowColor:    SystemControlBackgroundBaseMediumLowRevealBaseLowBackgroundBrush",
                    "SystemBaseMediumLowColor:    SystemControlHighlightBaseMediumLowRevealAccentBackgroundBrush",
                    "SystemBaseMediumLowColor:    SystemControlBackgroundBaseMediumLowRevealBorderBrush",
                    "SystemBaseMediumLowColor:    SystemControlHighlightBaseMediumLowRevealBorderBrush",
                    "SystemChromeAltLowColor:    SystemControlHighlightChromeAltLowBrush",
                    "SystemChromeBlackHighColor:    SystemControlBackgroundChromeBlackHighBrush",
                    "SystemChromeBlackHighColor:    SystemControlForegroundChromeBlackHighBrush",
                    "SystemChromeBlackHighColor:    SystemControlBackgroundChromeBlackHighRevealBorderBrush",
                    "SystemChromeBlackLowColor:    SystemControlBackgroundChromeBlackLowBrush",
                    "SystemChromeBlackMediumColor:    SystemControlBackgroundChromeBlackMediumBrush",
                    "SystemChromeBlackMediumColor:    SystemControlForegroundChromeBlackMediumBrush",
                    "SystemChromeBlackMediumColor:    SystemControlBackgroundChromeBlackMediumRevealChromeBorderBrush",
                    "SystemChromeBlackMediumLowColor:    SystemControlBackgroundChromeBlackMediumLowBrush",
                    "SystemChromeBlackMediumLowColor:    SystemControlForegroundChromeBlackMediumLowBrush",
                    "SystemChromeBlackMediumLowColor:    SystemControlPageTextChromeBlackMediumLowBrush",
                    "SystemChromeDisabledHighColor:    SystemControlDisabledChromeDisabledHighBrush",
                    "SystemChromeDisabledHighColor:    PersonPictureEllipseBadgeFillThemeBrush",
                    "SystemChromeDisabledLowColor:    SystemControlDisabledChromeDisabledLowBrush",
                    "SystemChromeDisabledLowColor:    SystemControlForegroundChromeDisabledLowBrush",
                    "SystemChromeGrayColor:    SystemControlForegroundChromeGrayBrush",
                    "SystemChromeHighColor:    SystemControlDisabledChromeHighBrush",
                    "SystemChromeHighColor:    SystemControlForegroundChromeHighBrush",
                    "SystemChromeHighColor:    SystemControlHighlightChromeHighBrush",
                    "SystemChromeLowColor:    SystemControlPageBackgroundChromeLowBrush",
                    "SystemChromeMediumColor:    SystemControlBackgroundChromeMediumBrush",
                    "SystemChromeMediumColor:    SystemControlForegroundChromeMediumBrush",
                    "SystemChromeMediumColor:    SystemControlAcrylicElementBrush",
                    "SystemChromeMediumColor:    SystemControlBackgroundChromeMediumRevealBorderBrush",
                    "SystemChromeMediumColor:    SystemControlRowGroupHeaderBackgroundMediumBrush",
                    "SystemChromeMediumColor:    NavigationViewExpandedPaneBackground",
                    "SystemChromeMediumColor:    ScrollBarTrackFill",
                    "SystemChromeMediumColor:    ScrollBarTrackFillPointerOver",
                    "SystemChromeMediumColor:    ScrollViewerScrollBarSeparatorBackground",
                    "SystemChromeMediumLowColor:    SystemControlBackgroundChromeMediumLowBrush",
                    "SystemChromeMediumLowColor:    SystemControlDisabledChromeMediumLowBrush",
                    "SystemChromeMediumLowColor:    SystemControlPageBackgroundChromeMediumLowBrush",
                    "SystemChromeMediumLowColor:    AcrylicBackgroundFillColorDefaultBrush",
                    "SystemChromeMediumLowColor:    SystemControlChromeMediumLowAcrylicElementMediumBrush",
                    "SystemChromeMediumLowColor:    SystemControlBackgroundChromeMediumLowRevealBorderBrush",
                    "SystemChromeWhiteColor:    SystemControlBackgroundChromeWhiteBrush",
                    "SystemChromeWhiteColor:    SystemControlForegroundChromeWhiteBrush",
                    "SystemChromeWhiteColor:    SystemControlHighlightAltChromeWhiteBrush",
                    "SystemChromeWhiteColor:    SystemControlHighlightChromeWhiteBrush",
                    "SystemChromeWhiteColor:    SystemControlBackgroundChromeWhiteRevealBorderBrush",
                    "SystemErrorTextColor:    SystemControlErrorTextForegroundBrush",
                    "SystemListLowColor:    SystemControlBackgroundListLowBrush",
                    "SystemListLowColor:    SystemControlForegroundListLowBrush",
                    "SystemListLowColor:    SystemControlHighlightListLowBrush",
                    "SystemListLowColor:    SystemControlPageBackgroundListLowBrush",
                    "SystemListLowColor:    SystemControlHighlightListLowRevealBackgroundBrush",
                    "SystemListLowColor:    SystemControlBackgroundListLowRevealBorderBrush",
                    "SystemListLowColor:    DataGridColumnHeaderHoveredBackgroundBrush",
                    "SystemListMediumColor:    SystemControlBackgroundListMediumBrush",
                    "SystemListMediumColor:    SystemControlDisabledListMediumBrush",
                    "SystemListMediumColor:    SystemControlForegroundListMediumBrush",
                    "SystemListMediumColor:    SystemControlHighlightListMediumBrush",
                    "SystemListMediumColor:    SystemControlHighlightListMediumRevealBackgroundBrush",
                    "SystemListMediumColor:    SystemControlHighlightListMediumRevealListLowBackgroundBrush",
                    "SystemListMediumColor:    SystemControlBackgroundListMediumRevealBorderBrush",
                    "SystemListMediumColor:    DataGridColumnHeaderPressedBackgroundBrush",
                    "SystemListMediumColor:    PersonPictureEllipseBadgeStrokeThemeBrush",
                    "ScrollBarPanningThumbBackgroundColor:    ScrollBarPanningThumbBackground",
                    "ScrollBarThumbBackgroundColor:    ScrollBarThumbBackground",
                    "SystemChromeMediumLowColor:    AcrylicInAppFillColorDefaultBrush",
                    "SystemChromeMediumLowColor:    AcrylicBackgroundFillColorBaseBrush",
                    "#000000:    SystemControlTransientBorderBrush",
                    "#F2F2F2:    AcrylicBackgroundFillColorDefaultInverseBrush",
                    "#FF000000:    ApplicationPageBackgroundThemeBrush",
                    "#FF555555:    WindowBorderInactive",
                    "#FF666666:    SystemControlGridLinesBaseMediumLowBrush",
                    "#FF707070:    WindowBorder",
                    "#FFFF00:    InvalidBrush",
                    "#FFFFFF:    SystemControlDefaultBrighteningBrush",
                    "Transparent:    SystemControlDisabledTransparentBrush",
                    "Transparent:    SystemControlForegroundTransparentBrush",
                    "Transparent:    SystemControlHighlightAltTransparentBrush",
                    "Transparent:    SystemControlHighlightTransparentBrush",
                    "Transparent:    SystemControlPageBackgroundTransparentBrush",
                    "Transparent:    SystemControlTransparentBrush",
                    "Transparent:    SystemControlTransparentRevealBackgroundBrush",
                    "Transparent:    SystemControlTransparentRevealListLowBorderBrush",
                    "Transparent:    SystemControlTransparentRevealBorderBrush",
                    "Transparent:    SystemControlForegroundRevealTransparentBorderBrush",
                    "Transparent:    SystemControlBackgroundTransparentRevealBorderBrush",
                    "Transparent:    SystemControlHighlightTransparentRevealBorderBrush",
                    "Transparent:    SystemControlHighlightAltTransparentRevealBorderBrush",
                    "Transparent:    ListViewItemRevealBorderBrush",
                    "Transparent:    ListViewItemRevealBorderBrushPointerOver",
                    "Transparent:    ListViewItemRevealBorderBrushPressed",
                    "Transparent:    GridViewItemRevealBorderBrush",
                    "Transparent:    FillerGridLinesBrush",
                    "Transparent:    DataGridCurrencyVisualPrimaryBrush",
                    "Transparent:    TitleBarButtonBackground",
                    "Transparent:    TitleBarButtonBackgroundInactive"})
                {
                    var parts = name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var brushName = parts[1];
                    items.Add(new ColorItem()
                    {
                        Name = name,
                        Brush = AppTheme.Instance.GetThemedBrush(brushName)
                    });
                }
                PaletteList.ItemsSource = items;
            }
        }

        public ViewState DeserializeViewState(XmlReader reader)
        {
            return null;
        }

        public void FocusQuickFilter()
        {
        }
        #endregion 

        #region IClipboardClient

        public bool CanCut => false;

        public bool CanCopy => true;

        public bool CanPaste => false;

        public bool CanDelete => false;
        public void Commit()
        {
        }

        public void Copy()
        {
            if (PaletteList.SelectedItem is ColorItem i)
            {
                Clipboard.SetText(i.Name);
            }
        }

        public void Cut()
        {
        }

        public void Delete()
        {
        }

        public void Paste()
        {
        }
        #endregion
    }

    public class ColorItem
    {
        public Brush Brush { get; set; }
        public string Name { get; set; }
    }
}
