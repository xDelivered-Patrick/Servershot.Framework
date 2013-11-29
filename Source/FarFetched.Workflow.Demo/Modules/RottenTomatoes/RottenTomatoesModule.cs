﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FarFetched.AzureWorkflow.Core;
using FarFetched.AzureWorkflow.Core.Enums;
using FarFetched.Workflow.Demo.Modules.RottenTomatoes;
using Newtonsoft.Json;

namespace FarFetched.Workflow.Demo
{
    public class RottenTomatoesModule : InitialWorkflowModule<Movie>
    {
        public RottenTomatoesModule() : base()
        {
            
        }

        public override async Task OnStart()
        {
            //get movies currently in the cinema
            string url = @"http://api.rottentomatoes.com/api/public/v1.0/lists/movies/in_theaters.json?page_limit=16&page=1&country=us&apikey={0}";
            string urlWithKey = String.Format(url, APISettings.Default.RottenTomatoesKey);


            HttpClient client = new HttpClient();
            var response = await client.GetAsync(urlWithKey);
            var jsonString = await response.Content.ReadAsStringAsync();
            var rottenTomatoesObject = await JsonConvert.DeserializeObjectAsync<RottenTomatoesJsonObjects.RootObject>(jsonString);

            foreach (var movieJson in rottenTomatoesObject.movies)
            {
                Movie movie = new Movie()
                {
                    MovieName = movieJson.title,
                    RottenTomatoesScore = movieJson.ratings.critics_score
                };

                base.SendTo(typeof (MetacriticModule), movie);
                base.RaiseProcessed(ProcessingResult.Success);
            }
        }
    }
}
