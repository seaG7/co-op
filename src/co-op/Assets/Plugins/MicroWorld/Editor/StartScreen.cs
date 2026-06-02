#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.Rendering;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace MicroWorldNS
{
	public class StartScreen : EditorWindow
	{
        public static string PrefStartUp => "MWLastSession" + Application.productName;
        public static string PrefForceUpdate => "MWForceUpdate" + Application.productName;

        [MenuItem(MenuManager.MainMenu + "Start Screen", false, 80 )]
		public static void Init()
		{
			StartScreen window = (StartScreen)GetWindow( typeof( StartScreen ), true, "Micro World Start Screen");
            window.maxSize = window.minSize = new Vector2( 650, 580);
			window.Show();
		}

		private static readonly string IconGUID = "a6d9468446f540c4fb36ebe566c66b32";
		private static readonly string BannerGUID = "4dc8b217ed47932408448f62db99f929";

		private static readonly string GuidURL = "https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?usp=sharing";

		private static readonly string DiscordURL = "https://discord.gg/tkPZ5sAMcc";
		private static readonly string ForumURL = "https://discussions.unity.com/t/released-microworld-procedural-level-generator/1541652";
        private static readonly string DemoAppURL = "https://drive.google.com/file/d/1AimXznXCfJK9eQK7PO0LiHYAEV4kZjEo/view?usp=sharing";

        private static readonly string StoreURL = "https://assetstore.unity.com/packages/slug/297049";

		private static readonly GUIContent LinksTitle = new GUIContent( "Links", "Need help? Reach us through our discord server or the offitial support Unity forum" );
        private static readonly GUIContent TitleSTR = new GUIContent("Micro World");

		bool m_startup = false;

		[NonSerialized]
		Texture textIcon = null;
		[NonSerialized]
		Texture webIcon = null;
        [NonSerialized]
        Texture warnIcon = null;
        

        GUIContent GuidButton = null;
		GUIContent DiscordButton = null;
		GUIContent ForumButton = null;
        GUIContent DemoAppButton = null;
        GUIContent ConvertToURPButton = null;

        GUIContent Icon = null;
		RenderTexture rt;

		[NonSerialized]
		GUIStyle m_buttonStyle = null;
		[NonSerialized]
		GUIStyle m_labelStyle = null;
		[NonSerialized]
		GUIStyle m_linkStyle = null;

		Texture2D m_newsImage = null;
		private bool m_infoDownloaded = false;
		private string m_newVersion = string.Empty;

		private void OnEnable()
		{
            rt = new RenderTexture( 16, 16, 0 );
			rt.Create();

			m_startup = EditorPrefs.GetBool( PrefStartUp, true );

			if( m_newsImage == null )
				m_newsImage = AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GUIDToAssetPath( BannerGUID ) );

			if( textIcon == null )
			{
				Texture icon = EditorGUIUtility.IconContent( "TextAsset Icon" ).image;
				var cache = RenderTexture.active;
				RenderTexture.active = rt;
				Graphics.Blit( icon, rt );
				RenderTexture.active = cache;
				textIcon = rt;
			}

			if( webIcon == null )
			{
				webIcon = EditorGUIUtility.IconContent( "BuildSettings.Web.Small" ).image;

                GuidButton = new GUIContent(" Documentation", webIcon);
                DiscordButton = new GUIContent( " Discord", webIcon );
				ForumButton = new GUIContent( " Unity Forum", webIcon );
                DemoAppButton = new GUIContent(" Demo App", webIcon);
            }

            if (warnIcon == null)
            {
                warnIcon = EditorGUIUtility.IconContent("console.warnicon.sml").image;

                ConvertToURPButton = new GUIContent(" Convert To URP", warnIcon);
            }

            

            if ( Icon == null )
			{
				Icon = new GUIContent( AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GUIDToAssetPath( IconGUID ) ) );
			}
		}

		private void OnDisable()
		{
			if( rt != null )
			{
				rt.Release();
				DestroyImmediate( rt );
			}
		}

		public void OnGUI()
		{
			if( !m_infoDownloaded )
			{
				m_infoDownloaded = true;
			}

			if( m_buttonStyle == null )
			{
				m_buttonStyle = new GUIStyle( GUI.skin.button );
				m_buttonStyle.alignment = TextAnchor.MiddleLeft;
			}

			if( m_labelStyle == null )
			{
				m_labelStyle = new GUIStyle( "BoldLabel" );
				m_labelStyle.margin = new RectOffset( 4, 4, 4, 4 );
				m_labelStyle.padding = new RectOffset( 2, 2, 2, 2 );
				m_labelStyle.fontSize = 13;
			}

			if( m_linkStyle == null )
			{
				var inv = AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GUIDToAssetPath( "1004d06b4b28f5943abdf2313a22790a" ) ); // find a better solution for transparent buttons
				m_linkStyle = new GUIStyle();
				m_linkStyle.normal.textColor = new Color( 0.2980392f, 0.4901961f, 1f );
				m_linkStyle.hover.textColor = Color.white;
				m_linkStyle.active.textColor = Color.grey;
				m_linkStyle.margin.top = 3;
				m_linkStyle.margin.bottom = 2;
				m_linkStyle.hover.background = inv;
				m_linkStyle.active.background = inv;
			}

			EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.ExpandWidth(true));
			{
				if (m_newsImage != null)
				{
                    var gc = new GUIContent(m_newsImage);
					int width = 650 - 9 - 8;
					width = Mathf.Min(m_newsImage.width, width);
					int height = m_newsImage.height;
					height = (int)((width + 8) * ((float)m_newsImage.height / (float)m_newsImage.width));

					EditorGUILayout.BeginVertical(GUILayout.Width(width));
					{
						Rect buttonRect = EditorGUILayout.GetControlRect(false, height);
						EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
						if (GUI.Button(buttonRect, gc, m_linkStyle))
						{
							Application.OpenURL(DiscordURL);
						}
					}

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
				{
                    GUILayout.Label(TitleSTR, m_labelStyle);

                    GUILayout.Label("Installed Version: " + VersionInfo.StaticToString());

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Check updates:");
                    if (GUILayout.Button("Asset Store", m_linkStyle))
                        Application.OpenURL(StoreURL);
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(7);
                    //GUILayout.Label(Icon);

                    //GUILayout.Label("Welcome to MicroWorld", "WordWrappedMiniLabel", GUILayout.ExpandHeight(true));

                    GUILayout.Label(LinksTitle, m_labelStyle);

                    GUI.skin.button.alignment = TextAnchor.MiddleLeft;

                    if (GUILayout.Button(GuidButton, GUILayout.ExpandWidth(true)))
                        Application.OpenURL(GuidURL);
                        
					if (GUILayout.Button(DiscordButton, GUILayout.ExpandWidth(true)))
						Application.OpenURL(DiscordURL);
						
					//if (GUILayout.Button(ForumButton, GUILayout.ExpandWidth(true)))
					//	Application.OpenURL(ForumURL);

                    if (GUILayout.Button(DemoAppButton, GUILayout.ExpandWidth(true)))
                        Application.OpenURL(DemoAppURL);

                    if (GraphicsSettings.defaultRenderPipeline != null)
                    {
						var typeName = GraphicsSettings.defaultRenderPipeline.GetType().Name;

                        if (typeName.Contains("HDRenderPipeline") || typeName.Contains("HighDefinition"))
                        {
                            // HDRP pipeline
                            GUILayout.Label("HDRP", m_labelStyle);

                            if (GUILayout.Button(ConvertToURPButton, GUILayout.ExpandWidth(true)))
                                Migration.Migrate(Migration.Dir.To_HDRP);

                            GUILayout.Label("It looks like you are using HDRP. Click the button to convert MicroWorld to HDRP pipeline.", "WordWrappedMiniLabel");
                        }
                        else
						{
                            // URP pipeline
                            GUILayout.Label("URP", m_labelStyle);

                            if (GUILayout.Button(ConvertToURPButton, GUILayout.ExpandWidth(true)))
                                Migration.Migrate(Migration.Dir.To_URP);

                            GUILayout.Label("It looks like you are using URP. Click the button to convert MicroWorld to URP pipeline.", "WordWrappedMiniLabel");
                        }
                    }

                    GUI.skin.button.alignment = TextAnchor.MiddleCenter;

                    //GUILayout.Label("NewsText", "WordWrappedMiniLabel", GUILayout.ExpandHeight(true));
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            {
                GUILayout.Label("Welcome to MicroWorld!", m_labelStyle);
                GUILayout.Label(
@"Thank you for choosing MicroWorld, the automatic procedural terrain generator for creating ready-to-use 
game levels.
Join our community on Discord and our forum to share your creations, ask questions, and find support.
We look forward to seeing the worlds you create!");

                GUILayout.Space(7);
                GUILayout.Label("Demo Scenes", m_labelStyle);

                var countInRow = 0;
				foreach (var sceneFile in GetDemoScenes())
				{
					if (countInRow % 5 == 0)
					{
                        if (countInRow > 0)
                            EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.ExpandWidth(true));
						countInRow = 0;
					}
                    
                    if (GUILayout.Button(Path.GetFileNameWithoutExtension(sceneFile), GUILayout.Width(125)))
						EditorSceneManager.OpenScene(sceneFile);

					countInRow++;
                }

				if (countInRow > 0)
					EditorGUILayout.EndHorizontal();

                GUILayout.Space(7);
                GUILayout.Label("Preferences", m_labelStyle);

                EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.ExpandWidth(true));
                {
                    if (GUILayout.Button("Micro World Preferences", GUILayout.Width(200)))
                        SettingsService.OpenProjectSettings(PrefsEditor.Path);
                }
                EditorGUILayout.EndHorizontal();
            }
			GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal( "ProjectBrowserBottomBarBg", GUILayout.ExpandWidth( true ), GUILayout.Height( 22 ) );
			{
                GUILayout.FlexibleSpace();
				EditorGUI.BeginChangeCheck();
				var cache = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 100;
				m_startup = EditorGUILayout.ToggleLeft( "Show At Startup", m_startup, GUILayout.Width( 120 ) );
				EditorGUIUtility.labelWidth = cache;
				if( EditorGUI.EndChangeCheck() )
				{
					EditorPrefs.SetBool( PrefStartUp, m_startup );
				}


			}
			EditorGUILayout.EndHorizontal();

			// Find a better way to update link buttons without repainting the window
			Repaint();
		}

        IEnumerable<string> GetDemoScenes()
        {
            var myDir = System.IO.Directory.EnumerateDirectories(Application.dataPath, "MicroWorld", SearchOption.AllDirectories).FirstOrDefault();
            if (myDir == null)
                yield break;

            var demoDir = Path.Combine(myDir, "Demo");
            foreach (var file in Directory.GetFiles(demoDir, "*.unity"))
                yield return file;
        }
    }

	[InitializeOnLoad]
	public class StartScreenLoader
	{
		static StartScreenLoader()
		{
			EditorApplication.update += Update;
		}

		static void Update()
		{
			EditorApplication.update -= Update;

			if( !EditorApplication.isPlayingOrWillChangePlaymode )
			{
				bool show = false;
				if( !EditorPrefs.HasKey( StartScreen.PrefStartUp) )
				{
					show = true;
					EditorPrefs.SetBool(StartScreen.PrefStartUp, true );
				}
				else
				{
					if( Time.realtimeSinceStartup < 10 )
					{
						show = EditorPrefs.GetBool(StartScreen.PrefStartUp, true );
					}
				}

				if( show )
					StartScreen.Init();
			}
		}
	}
}
#endif