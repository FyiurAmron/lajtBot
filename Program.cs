// ReSharper disable UnassignedField.Global
// ReSharper disable JoinDeclarationAndInitializer

namespace lajtBot {

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using DotNetEnv;

public static class HttpStatusCodeExtensions {
    public static bool isOK( this HttpStatusCode httpStatusCode ) {
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
            $"remaining day: {main_remaining_units} / 100 GB \t remaining night: (01-08AM) {second_remaining_units} / 200 GB";
    }
}

public class Program {
    private static readonly HttpClient httpClient = new();

    private static void Main( string[] args ) {
        Dictionary<string, string> env = Env.TraversePath().NoEnvVars().Load().ToDictionary();

        httpClient.BaseAddress = new Uri( "https://lajt-online.pl/" );
        var defaultRequestHeaders = httpClient.DefaultRequestHeaders;
        defaultRequestHeaders.Add( "X-Requested-With", "XMLHttpRequest" );
        defaultRequestHeaders.Add( "Brand", "lajt-online.pl" );

        HttpRequestMessage requestMessage;
        HttpResponseMessage responseMessage;

        requestMessage = new HttpRequestMessage {
            RequestUri = new UriRelative( $"api/authenticate" ),
            Method = HttpMethod.Post,
            Content = new FormUrlEncodedContent( new Dictionary<string, string>() {
                ["number"] = env["NUMBER"],
                ["password"] = env["PASSWORD"],
            } )
        };
        responseMessage = httpClient.Send( requestMessage );
        if ( !responseMessage.StatusCode.isOK() ) {
            throw new HttpRequestException( responseMessage.ToString() );
        }

        requestMessage = new HttpRequestMessage {
            RequestUri = new UriRelative( $"api/package/usage?feature={env["FEATURE"]}" ),
        };
        responseMessage = httpClient.Send( requestMessage );

        if ( !responseMessage.StatusCode.isOK() ) {
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

        Console.WriteLine( usage.data );
    }
}

}
