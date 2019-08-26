using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace CM.Server {
    public class Startup {
 
        IServer _Server;
        public Startup(IServer server) {
            _Server = server;
        }
        public void ConfigureServices(IServiceCollection services) {

            services.AddResponseCompression(options => {
                options.EnableForHttps = true;
                options.MimeTypes = new string[] {
                    "application/javascript",
                    "text/css",
                    "image/svg+xml",
                    "text/html",
                    "application/x-font-ttf" };
            });

            services.AddMvc()
              .AddJsonOptions(options => {
                  options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
              })
              .AddRazorPagesOptions(o => {
                  o.RootDirectory = "/wwwroot";
                  o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
              });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            app.UseResponseCompression();

            // Support nginx configurations
            app.UseForwardedHeaders(new ForwardedHeadersOptions {
                ForwardedForHeaderName = "X-Forwarded-For",
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
            app.UseStaticFiles(new StaticFileOptions() {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
                // ContentTypeProvider = provider,
                OnPrepareResponse = (e) => {
                    // note. gzip is always chunked
                    if (e.File.Name.EndsWith(".js")
                    || e.File.Name.EndsWith(".css")
                    || e.File.Name.EndsWith(".png")
                    || e.File.Name.EndsWith(".ttf")
                    || e.File.Name.EndsWith(".svg")) {
                        e.Context.Response.Headers.Add("cache-control", "public, max-age=1209600");
                    }
                }
            });
            
            app.UseWebSockets();
            app.Use(_Server.ProcessHttpRequest);
            app.UseMvc();
        }

       
    }
}
