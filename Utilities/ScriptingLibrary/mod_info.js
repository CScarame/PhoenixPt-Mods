({
   Id : "Sheepy.Scripting",
   Flags : "Library",
   Name : "Scripting Library",
   Description : 'Support "Eval" mod actions, "eval.cs" api.',
   LoadIndex : -300, // Load early to pre-load scripting engine
   Url : {
      "Nexus" : "https://nexusmods.com/phoenixpoint/mods/",
      "GitHub" : "https://github.com/Sheep-y/PhoenixPt-Mods/",
   },
   Requires : { Id: "Modnix", Min: 3 },
})