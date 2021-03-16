// ReSharper disable UnassignedField.Global
// ReSharper disable JoinDeclarationAndInitializer

namespace lajtBot {

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

using DotNetEnv;

public static class HttpStatusCodeExtensions {
    public static bool isOk( this HttpStatusCode httpStatusCode ) {
        return httpStatusCode == HttpStatusCode.OK;
    }
}

public class UriRelative : Uri {
    public UriRelative( string uriString ) : base( uriString, UriKind.Relative ) {
    }
}

public record Usage {
    public UsageData data;
    public int code;
    public string message;
}

public record UsageData {
    public string main_remaining_units;
    public string second_remaining_units;

    public override string ToString() {
        return
            $"remaining day: {main_remaining_units} / 100 GB\n" +
            $"remaining night: (01-08AM) {second_remaining_units} / 200 GB";
    }
}

public static class DebuggingExtensions {
    public static string toString<T>( this IEnumerable<T> iEnumerable ) {
        return string.Join( ",", iEnumerable );
    }

    public static string toString( this ParameterInfo parameterInfo ) {
        return $"{parameterInfo.ParameterType.Name} {parameterInfo.Name}";
    }

    public static string toString( this MethodBase methodBase ) {
        return $"{methodBase.Name}({methodBase.GetParameters().Select( p => p.toString() ).toString()})";
    }

    public static string toString( this StackFrame stackFrame ) {
        return ( stackFrame == null )
            ? "NO STACK FRAME!"
            : $"{stackFrame.GetMethod().toString()}"
            + $" @ {stackFrame.GetFileName()} : L{stackFrame.GetFileLineNumber()}:C{stackFrame.GetFileColumnNumber()}";
    }

    public static StackFrame GetFrame( this Exception ex, int index ) {
        return new StackTrace( ex, true ).GetFrame( index );
    }
}

public class Program {
    private static readonly HttpClient httpClient = new();

    private static Dictionary<string, string> env;

    private static string appName = AppDomain.CurrentDomain.FriendlyName; 

    [STAThread]
    private static void Main( string[] args ) {
        Form mainForm = null;
        try {
            mainForm = new() {
                Icon = Icon.ExtractAssociatedIcon( Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty ),
                Size = new Size( 0, 0 ),
                StartPosition = FormStartPosition.Manual,
                Location = new Point( 0, 0 ),
                Text = appName,
            };
            mainForm.Show();
            runApp( mainForm );
        } catch ( Exception ex ) {
            MessageBox.Show(
                mainForm,
                ex.Message,
                $"{appName} @ {ex.GetFrame( 0 ).toString()}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private static void runApp( Form mainForm ) {
        env = Env.TraversePath().NoEnvVars().Load().ToDictionary();

        httpClient.Timeout = TimeSpan.FromSeconds( 10 );
        httpClient.BaseAddress = new Uri( "https://lajt-online.pl/" );
        var defaultRequestHeaders = httpClient.DefaultRequestHeaders;
        defaultRequestHeaders.Add( "X-Requested-With", "XMLHttpRequest" );
        defaultRequestHeaders.Add( "Brand", "lajt-online.pl" );

        DialogResult dialogResult;
        do {
            Usage usage = fetchUsage();

            Console.WriteLine( usage.data );

            dialogResult = MessageBox.Show(
                mainForm,
                $"{usage.data}",
                appName,
                MessageBoxButtons.RetryCancel,
                MessageBoxIcon.Information
            );
        } while ( dialogResult == DialogResult.Retry );

        // end
    }

    private static Usage fetchUsage() {
        HttpRequestMessage requestMessage;
        HttpResponseMessage responseMessage;

        requestMessage = new HttpRequestMessage {
            RequestUri = new UriRelative( "api/authenticate" ),
            Method = HttpMethod.Post,
            Content = new FormUrlEncodedContent( new Dictionary<string, string>() {
                ["number"] = env["NUMBER"],
                ["password"] = env["PASSWORD"],
            } )
        };
        responseMessage = httpClient.Send( requestMessage );
        if ( !responseMessage.StatusCode.isOk() ) {
            throw new HttpRequestException( responseMessage.ToString() );
        }

        requestMessage = new HttpRequestMessage {
            RequestUri = new UriRelative( $"api/package/usage?feature={env["FEATURE"]}" ),
        };
        responseMessage = httpClient.Send( requestMessage );

        if ( !responseMessage.StatusCode.isOk() ) {
            throw new HttpRequestException( responseMessage.ToString() );
        }

        string usageJson = responseMessage.Content.ReadAsStringAsync().Result;
        JsonSerializerOptions jsonSerializerOptions = new() {
            IncludeFields = true,
        };
        var usage = JsonSerializer.Deserialize<Usage>( usageJson, jsonSerializerOptions );

        if ( usage is not { message: "success" } ) {
            throw new HttpRequestException( "usage.message not present in response" );
        }

        return usage;
    }
}

}
