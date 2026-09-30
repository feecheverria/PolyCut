Imports System.Text

Public Class PlotterJob
    Public Shared Function Compile(codes As IEnumerable(Of GCode), cfg As ProcessorConfiguration) As String
        PlotterSafety.ValidateConfiguration(cfg)
        Dim sb As New StringBuilder
        sb.AppendLine("; End3r Draw / PolyCut - tool job in millimetres")
        sb.AppendLine("; Home the machine before mounting the tool. This file does not home.")
        sb.AppendLine("; Physical offset = tip minus nozzle. Nozzle = tip minus physical offset.")
        sb.AppendLine("M104 S0")
        sb.AppendLine("M140 S0")
        sb.AppendLine("G21")
        sb.AppendLine("G90")
        For Each code In codes
            If code.E.HasValue OrElse (code.Code.HasValue AndAlso
                (code.Mode <> "G" OrElse (code.Code <> 0 AndAlso code.Code <> 1))) Then
                Throw New InvalidOperationException("El trabajo contiene comandos incompatibles con el módulo de lápiz/cortador.")
            End If
            sb.AppendLine(code.ToString())
        Next
        sb.AppendLine(GCode.GZ(cfg.TravelZ, cfg.ZSpeed * 60).ToString())
        sb.AppendLine("M104 S0")
        sb.AppendLine("M140 S0")
        sb.AppendLine("M400")
        Return sb.ToString()
    End Function
End Class
