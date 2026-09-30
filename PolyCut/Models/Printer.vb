Imports System.Globalization
Imports System.Text.RegularExpressions
Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Partial Public Class Printer : Inherits ObservableObject : Implements ISaveable

    <ImplementsProperty(GetType(ISaveable), NameOf(ISaveable.Version))>
    <ObservableProperty> Private _Version As Single = 0.1

    <ImplementsProperty(GetType(ISaveable), NameOf(ISaveable.Name))>
    <ObservableProperty> Private _Name As String = "Ender 3 Pro + Pen Holder"

    <NotifyPropertyChangedFor(NameOf(BedRect))>
    <ObservableProperty> Private _BedWidth As Decimal = 235

    <NotifyPropertyChangedFor(NameOf(BedRect))>
    <ObservableProperty> Private _BedHeight As Decimal = 235

    Private Sub OnBedWidthChanged(oldValue As Decimal, newValue As Decimal)
        WorkingOffsetX = If(WorkingOffsetX < newValue, WorkingOffsetX, 0)
        WorkingWidth = If(WorkingWidth < newValue - WorkingOffsetX, WorkingWidth, newValue - WorkingOffsetX)
    End Sub

    Private Sub OnBedHeightChanged(oldValue As Decimal, newValue As Decimal)
        WorkingOffsetY = If(WorkingOffsetY < newValue, WorkingOffsetY, 0)
        WorkingHeight = If(WorkingHeight < newValue - WorkingOffsetY, WorkingHeight, newValue - WorkingOffsetY)
    End Sub

    Private _WorkingOffsetX As Decimal = 30
    Private _WorkingOffsetY As Decimal = 30
    Private _WorkingWidth As Decimal = 150
    Private _WorkingHeight As Decimal = 150

    Public Property WorkingOffsetX As Decimal
        Get
            Return _WorkingOffsetX
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingOffsetX, Math.Max(0, Math.Min(value, BedWidth)), NameOf(WorkingOffsetX))
            OnPropertyChanged(NameOf(WorkingRect))
            WorkingWidth = If(WorkingWidth <= BedWidth - WorkingOffsetX, WorkingWidth, BedWidth - WorkingOffsetX)
        End Set
    End Property

    Public Property WorkingOffsetY As Decimal
        Get
            Return _WorkingOffsetY
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingOffsetY, Math.Max(0, Math.Min(value, BedHeight)), NameOf(WorkingOffsetY))
            OnPropertyChanged(NameOf(WorkingRect))
            WorkingHeight = If(WorkingHeight <= BedHeight - WorkingOffsetY, WorkingHeight, BedHeight - WorkingOffsetY)
        End Set
    End Property

    Public Property WorkingWidth As Decimal
        Get
            Return _WorkingWidth
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingWidth, Math.Max(0, Math.Min(value, BedWidth - WorkingOffsetX)), NameOf(WorkingWidth))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public Property WorkingHeight As Decimal
        Get
            Return _WorkingHeight
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingHeight, Math.Max(0, Math.Min(value, BedHeight - WorkingOffsetY)), NameOf(WorkingHeight))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public ReadOnly Property BedRect As Rect
        Get
            Return New Rect(0, 0, BedWidth, BedHeight)
        End Get
    End Property

    Public ReadOnly Property WorkingRect As Rect
        Get
            Return New Rect(WorkingOffsetX, WorkingOffsetY, WorkingWidth, WorkingHeight)
        End Get
    End Property

    <ObservableProperty> Private _StartGCode As String = ""
    <ObservableProperty> Private _EndGCode As String = $""
    <ObservableProperty> Private _PreviewStartGCode As String = ""
    <ObservableProperty> Private _PreviewEndGCode As String = $""

    <ObservableProperty> Private _ToolOffsetX As Decimal = 0
    <ObservableProperty> Private _ToolOffsetY As Decimal = 0

    ' Physical tip minus nozzle. A pen to the left has a NEGATIVE X offset.
    <ObservableProperty> Private _CalibrationConfirmed As Boolean = False
    <ObservableProperty> Private _AttachedToolMode As ProcessorConfiguration.ToolMode = ProcessorConfiguration.ToolMode.Draw
    <ObservableProperty> Private _NozzleMinX As Decimal = 0
    <ObservableProperty> Private _NozzleMinY As Decimal = 0
    <ObservableProperty> Private _NozzleMaxX As Decimal = 220
    <ObservableProperty> Private _NozzleMaxY As Decimal = 220
    <ObservableProperty> Private _MaximumZ As Decimal = 250
    <ObservableProperty> Private _ContactZ As Double = 0
    <ObservableProperty> Private _LiftZ As Double = 5

    Public ReadOnly Property SafeCanvasRect As Rect
        Get
            Return New Rect(WorkingOffsetX, BedHeight - WorkingOffsetY - WorkingHeight, WorkingWidth, WorkingHeight)
        End Get
    End Property

    Public Function CreateSafetySettings() As PlotterSafetySettings
        If NozzleMaxX <= NozzleMinX OrElse NozzleMaxY <= NozzleMinY Then
            Throw New InvalidOperationException("Los límites máximos de la boquilla deben superar los mínimos.")
        End If
        Return New PlotterSafetySettings With {
            .CalibrationConfirmed = CalibrationConfirmed, .SafeTipArea = WorkingRect,
            .NozzleArea = New Rect(NozzleMinX, NozzleMinY, NozzleMaxX - NozzleMinX, NozzleMaxY - NozzleMinY),
            .MaximumZ = MaximumZ, .BedWidth = BedWidth, .BedHeight = BedHeight}
    End Function

    Protected Overrides Sub OnPropertyChanged(e As System.ComponentModel.PropertyChangedEventArgs)
        MyBase.OnPropertyChanged(e)
        If e.PropertyName = NameOf(WorkingRect) OrElse e.PropertyName = NameOf(BedRect) Then
            MyBase.OnPropertyChanged(New System.ComponentModel.PropertyChangedEventArgs(NameOf(SafeCanvasRect)))
        End If
    End Sub

    Public Function Clone() As Printer
        Dim p As New Printer
        p.CopyFrom(Me)
        Return p
    End Function

    Public Sub CopyFrom(other As Printer)
        If other Is Nothing Then Return

        Me.Version = other.Version
        Me.Name = other.Name
        Me.BedWidth = other.BedWidth
        Me.BedHeight = other.BedHeight
        Me.WorkingOffsetX = other.WorkingOffsetX
        Me.WorkingOffsetY = other.WorkingOffsetY
        Me.WorkingWidth = other.WorkingWidth
        Me.WorkingHeight = other.WorkingHeight
        Me.StartGCode = other.StartGCode
        Me.EndGCode = other.EndGCode
        Me.PreviewStartGCode = other.PreviewStartGCode
        Me.PreviewEndGCode = other.PreviewEndGCode
        Me.ToolOffsetX = other.ToolOffsetX
        Me.ToolOffsetY = other.ToolOffsetY
        Me.AttachedToolMode = other.AttachedToolMode
        Me.NozzleMinX = other.NozzleMinX
        Me.NozzleMinY = other.NozzleMinY
        Me.NozzleMaxX = other.NozzleMaxX
        Me.NozzleMaxY = other.NozzleMaxY
        Me.MaximumZ = other.MaximumZ
        Me.ContactZ = other.ContactZ
        Me.LiftZ = other.LiftZ
        Me.CalibrationConfirmed = other.CalibrationConfirmed
    End Sub

End Class

