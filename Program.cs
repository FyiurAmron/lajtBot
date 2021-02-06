namespace lajtBot {

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using DotNetEnv;

public class UriRelative : Uri {
    public UriRelative( string uriString ) : base( uriString, UriKind.Relative ) {
    }
}

public class Usage {
    public UsageData data;
    public int code;
    public string message;
}

public class UsageData {
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
        if ( (int) responseMessage.StatusCode != 200 ) {
            throw new HttpRequestException( responseMessage.ToString() );
        }

        requestMessage = new HttpRequestMessage {
            RequestUri = new UriRelative( $"api/package/usage?feature={env["FEATURE"]}" ),
        };
        responseMessage = httpClient.Send( requestMessage );

        if ( (int) responseMessage.StatusCode != 200 ) {
            throw new HttpRequestException( responseMessage.ToString() );
        }

        string usageJson = responseMessage.Content.ReadAsStringAsync().Result;
        JsonSerializerOptions jsonSerializerOptions = new() {
            IncludeFields = true,
        };
        var usage = JsonSerializer.Deserialize<Usage>( usageJson, jsonSerializerOptions );

        if ( usage.message != "success" ) {
            throw new HttpRequestException( "usage.message != \"success\"" );
        }

        Console.WriteLine( usage.data );
    }
}

}
