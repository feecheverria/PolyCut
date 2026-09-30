Imports System.Windows
Imports CommunityToolkit.Mvvm.ComponentModel

' Paper coordinates use the same top-left origin as the design canvas (millimetres).
Partial Public Class PaperLayout
    Inherits ObservableObject

    <ObservableProperty> Private _Preset As String = "Custom 13 x 9 cm"
    <NotifyPropertyChangedFor(NameOf(CanvasRect))>
    <ObservableProperty> Private _Left As Double = 40
    <NotifyPropertyChangedFor(NameOf(CanvasRect))>
    <ObservableProperty> Private _Top As Double = 60
    <NotifyPropertyChangedFor(NameOf(CanvasRect))>
    <ObservableProperty> Private _Width As Double = 90
    <NotifyPropertyChangedFor(NameOf(CanvasRect))>
    <ObservableProperty> Private _Height As Double = 130

    Public ReadOnly Property CanvasRect As Rect
        Get
            If Not Double.IsFinite(Left) OrElse Not Double.IsFinite(Top) OrElse
                Not Double.IsFinite(Width) OrElse Not Double.IsFinite(Height) OrElse Width <= 0 OrElse Height <= 0 Then Return Rect.Empty
            Return New Rect(Left, Top, Width, Height)
        End Get
    End Property

    Public Function Clone() As PaperLayout
        Return New PaperLayout With {.Preset = Preset, .Left = Left, .Top = Top, .Width = Width, .Height = Height}
    End Function
End Class

' Coordinates here refer to the physical tip and the nozzle, with a bottom-left origin.
Public Class PlotterSafetySettings
    Public Property CalibrationConfirmed As Boolean = False
    Public Property SafeTipArea As Rect = New Rect(30, 30, 150, 150)
    Public Property NozzleArea As Rect = New Rect(0, 0, 220, 220)
    Public Property MaximumZ As Double = 250
    Public Property BedWidth As Double = 235
    Public Property BedHeight As Double = 235

    Public Function Clone() As PlotterSafetySettings
        Return DirectCast(MemberwiseClone(), PlotterSafetySettings)
    End Function
End Class

Public Class PlotterSafety
    Public Shared Sub ValidateConfiguration(cfg As ProcessorConfiguration)
        Dim safety = cfg.PlotterSafety
        If safety Is Nothing OrElse Not safety.CalibrationConfirmed Then
            Throw New InvalidOperationException("Calibración pendiente: mide el offset de la punta, el área segura, los límites de movimiento y las alturas Z antes de generar G-code.")
        End If
        Dim values = {cfg.WorkZ, cfg.TravelZ, cfg.ZSpeed, cfg.WorkSpeed, cfg.TravelSpeed,
                      cfg.ToolOffsetX, cfg.ToolOffsetY, cfg.PassHeightDelta, safety.MaximumZ,
                      cfg.WorkAreaWidth, cfg.WorkAreaHeight}
        If values.Any(Function(v) Not Double.IsFinite(v)) OrElse cfg.WorkSpeed <= 0 OrElse
            cfg.TravelSpeed <= 0 OrElse cfg.ZSpeed <= 0 OrElse cfg.Passes < 1 OrElse cfg.Passes > 100 OrElse
            cfg.WorkZ < 0 OrElse cfg.TravelZ <= cfg.WorkZ OrElse cfg.TravelZ > safety.MaximumZ Then
            Throw New InvalidOperationException("Revisa las alturas Z, las velocidades y el número de pasadas. Z de traslado debe superar Z de contacto.")
        End If
        Dim lastZ = cfg.WorkZ + (cfg.Passes - 1) * cfg.PassHeightDelta
        If lastZ < 0 OrElse lastZ >= cfg.TravelZ Then Throw New InvalidOperationException("Las pasadas salen del rango Z calibrado.")
        If cfg.SelectedToolMode = ProcessorConfiguration.ToolMode.Draw AndAlso (cfg.Passes <> 1 OrElse cfg.PassHeightDelta <> 0) Then
            Throw New InvalidOperationException("El módulo de lápiz usa una pasada, sin descenso Z entre pasadas.")
        End If
        Dim bed As New Rect(0, 0, safety.BedWidth, safety.BedHeight)
        If Not ValidRect(safety.SafeTipArea) OrElse Not ValidRect(safety.NozzleArea) OrElse
            Not bed.Contains(safety.SafeTipArea) Then Throw New InvalidOperationException("El área segura debe estar dentro de la cama física.")
    End Sub

    Public Shared Sub ValidatePaths(lines As List(Of GeoLine), cfg As ProcessorConfiguration, Optional requirePaper As Boolean = True)
        ValidateConfiguration(cfg)
        If lines Is Nothing OrElse lines.Count = 0 Then Throw New InvalidOperationException("No hay trayectorias para dibujar.")
        Dim paper = cfg.Paper?.CanvasRect
        If requirePaper AndAlso (Not paper.HasValue OrElse Not ValidRect(paper.Value)) Then
            Throw New InvalidOperationException("Revisa el tamaño y la posición del papel.")
        End If
        For i = 0 To lines.Count - 1
            For Each p In {New Point(lines(i).X1, lines(i).Y1), New Point(lines(i).X2, lines(i).Y2)}
                If Not Double.IsFinite(p.X) OrElse Not Double.IsFinite(p.Y) Then Throw New InvalidOperationException("La trayectoria contiene coordenadas inválidas.")
                Dim tip As New Point(p.X, cfg.WorkAreaHeight - p.Y)
                Dim nozzle As New Point(tip.X + cfg.ToolOffsetX, tip.Y + cfg.ToolOffsetY)
                If Not cfg.PlotterSafety.SafeTipArea.Contains(tip) Then
                    Throw New InvalidOperationException($"Trayectoria {i + 1} fuera del área segura. Mueve o reduce el diseño, o corrige la calibración.")
                End If
                If Not cfg.PlotterSafety.NozzleArea.Contains(nozzle) Then
                    Throw New InvalidOperationException($"Trayectoria {i + 1} fuera del alcance de la boquilla al compensar el offset. Revisa el perfil y la ubicación.")
                End If
                If requirePaper AndAlso Not paper.Value.Contains(p) Then
                    Throw New InvalidOperationException($"Trayectoria {i + 1} fuera del papel. Mueve o reduce el diseño.")
                End If
            Next
        Next
    End Sub

    Private Shared Function ValidRect(rect As Rect) As Boolean
        Return Not rect.IsEmpty AndAlso rect.Width > 0 AndAlso rect.Height > 0 AndAlso
            {rect.X, rect.Y, rect.Width, rect.Height}.All(Function(v) Double.IsFinite(v))
    End Function
End Class

Public Class CalibrationPatterns
    ' Center a modest pattern inside the configured area; nothing here asserts physical calibration.
    Public Shared Function Create(cfg As ProcessorConfiguration, grid As Boolean) As List(Of GeoLine)
        Dim area = cfg.PlotterSafety.SafeTipArea
        Dim size = Math.Min(40, Math.Min(area.Width, area.Height) - 10)
        If size < 10 Then Throw New InvalidOperationException("El área segura necesita al menos 20 x 20 mm para la calibración.")
        Dim x = area.X + (area.Width - size) / 2
        Dim y = cfg.WorkAreaHeight - (area.Y + (area.Height + size) / 2)
        Dim result As New List(Of GeoLine) From {
            New GeoLine(x, y, x + size, y), New GeoLine(x + size, y, x + size, y + size),
            New GeoLine(x + size, y + size, x, y + size), New GeoLine(x, y + size, x, y)}
        If grid Then
            For i = 1 To 3
                Dim stepSize = size * i / 4
                result.Add(New GeoLine(x + stepSize, y, x + stepSize, y + size))
                result.Add(New GeoLine(x, y + stepSize, x + size, y + stepSize))
            Next
        End If
        Return result
    End Function
End Class
