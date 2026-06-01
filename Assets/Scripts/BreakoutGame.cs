using System;
using UnityEngine;

namespace ZBreakOut
{
    public enum GameState
    {
        Ready,
        Playing,
        Won,
        GameOver
    }

    public sealed class BreakoutGame : MonoBehaviour
    {
        private const float BoardHalfWidth = 6.65f;
        private const int StartingLives = 3;

        private readonly Color _background = FromHex("#071426");
        private readonly Color _panel = FromHex("#0D2540");
        private readonly Color _cyan = FromHex("#4CE6E6");
        private readonly Color _cream = FromHex("#FFF2C9");
        private readonly Color _danger = FromHex("#FF5C7A");

        private Camera _camera;
        private Sprite _rectangleSprite;
        private Sprite _circleSprite;
        private Texture2D _whiteTexture;
        private AudioSource _audioSource;
        private AudioClip _bounceClip;
        private AudioClip _brickClip;
        private AudioClip _loseClip;
        private AudioClip _winClip;
        private Transform _brickRoot;
        private PaddleController _paddle;
        private BallController _ball;
        private GUIStyle _brandStyle;
        private GUIStyle _hudStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _smallCenterStyle;
        private int _score;
        private int _lives;
        private int _bricksRemaining;

        public Camera GameCamera
        {
            get { return _camera; }
        }

        public GameState State { get; private set; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            CreateSprites();
            CreateAudio();
            CreateCamera();
            CreateBackdrop();
            CreateArena();
            CreatePaddle();
            CreateBall();
            StartNewGame();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }

            if (State == GameState.Playing)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    StartNewGame();
                }

                return;
            }

            if (ConfirmPressed())
            {
                if (State == GameState.Won || State == GameState.GameOver)
                {
                    StartNewGame();
                }

                BeginRound();
            }
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            float width = Screen.width;
            float height = Screen.height;

            DrawPanel(new Rect(0f, 0f, width, 68f), new Color(0.035f, 0.11f, 0.2f, 0.97f));
            GUI.Label(new Rect(24f, 14f, width * 0.4f, 42f), "Z BREAKOUT", _brandStyle);
            GUI.Label(new Rect(width * 0.36f, 16f, width * 0.28f, 40f), "PUNTOS  " + _score.ToString("0000"), _hudStyle);
            GUI.Label(new Rect(width - 230f, 16f, 206f, 40f), "VIDAS  " + Hearts(), _hudStyle);

            GUI.Label(
                new Rect(0f, height - 44f, width, 30f),
                "MOVER: A / D O FLECHAS    |    LANZAR: ESPACIO    |    REINICIAR: R    |    SALIR: ESC",
                _smallCenterStyle);

            if (State == GameState.Playing)
            {
                return;
            }

            Rect modal = new Rect(width * 0.5f - 270f, height * 0.5f - 88f, 540f, 176f);
            DrawPanel(modal, new Color(0.035f, 0.13f, 0.23f, 0.94f));

            string title;
            string message;
            switch (State)
            {
                case GameState.Won:
                    title = "NIVEL COMPLETADO";
                    message = "PRESIONA ESPACIO PARA JUGAR DE NUEVO";
                    break;
                case GameState.GameOver:
                    title = "FIN DE LA PARTIDA";
                    message = "PRESIONA ESPACIO PARA INTENTARLO OTRA VEZ";
                    break;
                default:
                    title = "ROMPE TODOS LOS BLOQUES";
                    message = "PRESIONA ESPACIO PARA LANZAR LA PELOTA";
                    break;
            }

            GUI.Label(new Rect(modal.x + 10f, modal.y + 34f, modal.width - 20f, 46f), title, _centerStyle);
            GUI.Label(new Rect(modal.x + 10f, modal.y + 96f, modal.width - 20f, 32f), message, _smallCenterStyle);
        }

        public void HitBrick(Brick brick)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            brick.TakeHit();
        }

        public void BrickDamaged(int points)
        {
            _score += points;
            PlayClip(_brickClip);
        }

        public void BrickDestroyed(int points)
        {
            _score += points;
            _bricksRemaining--;
            PlayClip(_brickClip);

            if (_bricksRemaining <= 0)
            {
                State = GameState.Won;
                _ball.Freeze();
                PlayClip(_winClip);
            }
        }

        public void LoseLife()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            _lives--;
            PlayClip(_loseClip);

            if (_lives <= 0)
            {
                State = GameState.GameOver;
                _ball.Freeze();
                return;
            }

            State = GameState.Ready;
            _paddle.ResetPosition();
            _ball.ResetToPaddle();
        }

        public void PlayBounce()
        {
            PlayClip(_bounceClip);
        }

        private void StartNewGame()
        {
            _score = 0;
            _lives = StartingLives;
            State = GameState.Ready;
            RebuildLevel();
            _paddle.ResetPosition();
            _ball.ResetToPaddle();
        }

        private void BeginRound()
        {
            State = GameState.Playing;
            _ball.Launch();
        }

        private void RebuildLevel()
        {
            if (_brickRoot != null)
            {
                Destroy(_brickRoot.gameObject);
            }

            _brickRoot = new GameObject("Bricks").transform;
            _brickRoot.SetParent(transform);
            _bricksRemaining = 0;

            Color[] rowColors =
            {
                FromHex("#FF5C7A"),
                FromHex("#FF9F68"),
                FromHex("#FFD166"),
                FromHex("#76E39F"),
                FromHex("#4CE6E6"),
                FromHex("#7CA8FF")
            };

            const int rows = 6;
            const int columns = 10;
            const float spacingX = 1.22f;
            const float spacingY = 0.72f;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float x = (column - (columns - 1) * 0.5f) * spacingX;
                    float y = 4.9f - row * spacingY;
                    CreateBrick(new Vector2(x, y), rowColors[row], row < 2 ? 2 : 1, (rows - row) * 10);
                    _bricksRemaining++;
                }
            }
        }

        private void CreateBackdrop()
        {
            GameObject background = CreateRectangle("Background", Vector2.zero, new Vector2(16f, 18f), _background, -20, transform);
            background.transform.position = new Vector3(0f, 0f, 4f);

            System.Random random = new System.Random(2204);
            for (int index = 0; index < 58; index++)
            {
                float x = Mathf.Lerp(-6.2f, 6.2f, (float)random.NextDouble());
                float y = Mathf.Lerp(-6.8f, 6.5f, (float)random.NextDouble());
                float size = Mathf.Lerp(0.025f, 0.085f, (float)random.NextDouble());
                Color color = index % 4 == 0 ? _cyan : _cream;
                color.a = Mathf.Lerp(0.28f, 0.8f, (float)random.NextDouble());
                CreateRectangle("Star", new Vector2(x, y), new Vector2(size, size), color, -10, transform);
            }

            CreateRectangle("Header Line", new Vector2(0f, 6.1f), new Vector2(12.8f, 0.045f), new Color(0.3f, 0.9f, 0.9f, 0.24f), -5, transform);
        }

        private void CreateArena()
        {
            CreateWall("Left Wall", new Vector2(-BoardHalfWidth, 0f), new Vector2(0.24f, 13.9f));
            CreateWall("Right Wall", new Vector2(BoardHalfWidth, 0f), new Vector2(0.24f, 13.9f));
            CreateWall("Top Wall", new Vector2(0f, 6.85f), new Vector2(13.5f, 0.24f));

            GameObject dangerLine = CreateRectangle("Danger Line", new Vector2(0f, -7.05f), new Vector2(13.2f, 0.05f), new Color(1f, 0.25f, 0.4f, 0.52f), -2, transform);
            dangerLine.AddComponent<BoxCollider2D>().isTrigger = true;
            dangerLine.AddComponent<LoseZone>();
        }

        private void CreateWall(string objectName, Vector2 position, Vector2 size)
        {
            GameObject wall = CreateRectangle(objectName, position, size, new Color(0.29f, 0.9f, 0.9f, 0.62f), -1, transform);
            wall.AddComponent<BoxCollider2D>();
        }

        private void CreatePaddle()
        {
            GameObject paddleObject = CreateRectangle("Paddle", new Vector2(0f, -6.25f), new Vector2(2.45f, 0.4f), _cyan, 5, transform);
            paddleObject.AddComponent<BoxCollider2D>();
            AddChildRectangle(paddleObject, "Paddle Shadow", new Vector2(0.04f, -0.2f), Vector2.one, new Color(0f, 0f, 0f, 0.34f), 2);
            AddChildRectangle(paddleObject, "Paddle Highlight", new Vector2(0f, 0.21f), new Vector2(0.82f, 0.16f), _cream, 6);
            _paddle = paddleObject.AddComponent<PaddleController>();
            _paddle.Initialize(this, BoardHalfWidth - 0.25f, 2.45f);
        }

        private void CreateBall()
        {
            GameObject ballObject = new GameObject("Ball");
            ballObject.transform.SetParent(transform);
            ballObject.transform.localScale = Vector3.one * 0.58f;

            SpriteRenderer renderer = ballObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _circleSprite;
            renderer.color = _cream;
            renderer.sortingOrder = 8;

            CircleCollider2D circleCollider = ballObject.AddComponent<CircleCollider2D>();
            PhysicsMaterial2D bounceMaterial = new PhysicsMaterial2D("Ball Bounce");
            bounceMaterial.bounciness = 1f;
            bounceMaterial.friction = 0f;
            circleCollider.sharedMaterial = bounceMaterial;

            Rigidbody2D body = ballObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            Shader trailShader = Shader.Find("Sprites/Default");
            if (trailShader != null)
            {
                TrailRenderer trail = ballObject.AddComponent<TrailRenderer>();
                trail.material = new Material(trailShader);
                trail.startColor = new Color(0.3f, 0.95f, 0.95f, 0.72f);
                trail.endColor = new Color(0.3f, 0.95f, 0.95f, 0f);
                trail.time = 0.16f;
                trail.startWidth = 0.24f;
                trail.endWidth = 0.03f;
                trail.sortingOrder = 3;
            }

            _ball = ballObject.AddComponent<BallController>();
            _ball.Initialize(this, _paddle, body);
        }

        private void CreateBrick(Vector2 position, Color color, int health, int points)
        {
            GameObject brickObject = CreateRectangle("Brick", position, new Vector2(1.08f, 0.52f), color, 3, _brickRoot);
            brickObject.AddComponent<BoxCollider2D>();
            AddChildRectangle(brickObject, "Shadow", new Vector2(0.055f, -0.15f), Vector2.one, new Color(0f, 0f, 0f, 0.28f), 1);
            AddChildRectangle(brickObject, "Highlight", new Vector2(0f, 0.24f), new Vector2(0.78f, 0.12f), new Color(1f, 1f, 1f, 0.62f), 4);

            Brick brick = brickObject.AddComponent<Brick>();
            brick.Initialize(this, brickObject.GetComponent<SpriteRenderer>(), health, points);
        }

        private GameObject CreateRectangle(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder, Transform parent)
        {
            GameObject rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent);
            rectangle.transform.position = position;
            rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = _rectangleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return rectangle;
        }

        private void AddChildRectangle(GameObject parent, string objectName, Vector2 localPosition, Vector2 localScale, Color color, int sortingOrder)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = _rectangleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private void CreateSprites()
        {
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.name = "Runtime White Pixel";
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _rectangleSprite = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            const int textureSize = 64;
            Texture2D circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            circleTexture.name = "Runtime Ball";
            Color[] pixels = new Color[textureSize * textureSize];
            Vector2 center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
            float radius = textureSize * 0.48f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    pixels[y * textureSize + x] = distance <= radius ? Color.white : Color.clear;
                }
            }

            circleTexture.SetPixels(pixels);
            circleTexture.Apply();
            circleTexture.filterMode = FilterMode.Bilinear;
            _circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        }

        private void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, -0.1f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7.5f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _background;
        }

        private void CreateAudio()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0.24f;
            _bounceClip = CreateTone("Bounce", 520f, 0.045f);
            _brickClip = CreateTone("Brick", 760f, 0.065f);
            _loseClip = CreateTone("Lose", 175f, 0.24f);
            _winClip = CreateTone("Win", 980f, 0.32f);
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float time = (float)index / sampleRate;
                float fade = 1f - (float)index / sampleCount;
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * fade * 0.32f;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void EnsureGuiStyles()
        {
            if (_brandStyle != null)
            {
                return;
            }

            _brandStyle = new GUIStyle(GUI.skin.label);
            _brandStyle.fontSize = 28;
            _brandStyle.fontStyle = FontStyle.Bold;
            _brandStyle.normal.textColor = _cyan;

            _hudStyle = new GUIStyle(GUI.skin.label);
            _hudStyle.fontSize = 22;
            _hudStyle.fontStyle = FontStyle.Bold;
            _hudStyle.alignment = TextAnchor.MiddleCenter;
            _hudStyle.normal.textColor = _cream;

            _centerStyle = new GUIStyle(_hudStyle);
            _centerStyle.fontSize = 30;
            _centerStyle.normal.textColor = _cyan;

            _smallCenterStyle = new GUIStyle(_hudStyle);
            _smallCenterStyle.fontSize = 15;
            _smallCenterStyle.normal.textColor = _cream;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTexture);
            GUI.color = previousColor;
        }

        private string Hearts()
        {
            string hearts = string.Empty;
            for (int index = 0; index < StartingLives; index++)
            {
                hearts += index < _lives ? "● " : "○ ";
            }

            return hearts;
        }

        private static bool ConfirmPressed()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
        }

        private static Color FromHex(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
        }
    }

    public sealed class PaddleController : MonoBehaviour
    {
        private BreakoutGame _game;
        private float _movementLimit;
        private float _width;
        private Vector3 _lastMousePosition;
        private bool _mouseInitialized;

        public float Width
        {
            get { return _width; }
        }

        public void Initialize(BreakoutGame game, float movementLimit, float width)
        {
            _game = game;
            _movementLimit = movementLimit;
            _width = width;
        }

        public void ResetPosition()
        {
            transform.position = new Vector2(0f, -6.25f);
        }

        private void Update()
        {
            float x = transform.position.x;
            float keyboard = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keyboard) > 0.01f)
            {
                x += keyboard * 10.5f * Time.deltaTime;
            }
            else
            {
                Vector3 mousePosition = Input.mousePosition;
                if (_mouseInitialized && (mousePosition - _lastMousePosition).sqrMagnitude > 3f)
                {
                    Vector3 worldPosition = _game.GameCamera.ScreenToWorldPoint(mousePosition);
                    x = worldPosition.x;
                }

                _lastMousePosition = mousePosition;
                _mouseInitialized = true;
            }

            float halfWidth = _width * 0.5f;
            x = Mathf.Clamp(x, -_movementLimit + halfWidth, _movementLimit - halfWidth);
            transform.position = new Vector2(x, transform.position.y);
        }
    }

    public sealed class BallController : MonoBehaviour
    {
        private const float MovementSpeed = 8.4f;

        private BreakoutGame _game;
        private PaddleController _paddle;
        private Rigidbody2D _body;
        private bool _attached;

        public void Initialize(BreakoutGame game, PaddleController paddle, Rigidbody2D body)
        {
            _game = game;
            _paddle = paddle;
            _body = body;
        }

        public void ResetToPaddle()
        {
            _attached = true;
            _body.linearVelocity = Vector2.zero;
            transform.position = _paddle.transform.position + Vector3.up * 0.55f;
        }

        public void Launch()
        {
            if (!_attached)
            {
                return;
            }

            _attached = false;
            _body.linearVelocity = new Vector2(0.68f, 1f).normalized * MovementSpeed;
        }

        public void Freeze()
        {
            _attached = false;
            _body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            if (_attached)
            {
                transform.position = _paddle.transform.position + Vector3.up * 0.55f;
            }
        }

        private void FixedUpdate()
        {
            if (_attached || _game.State != GameState.Playing)
            {
                return;
            }

            Vector2 direction = _body.linearVelocity.normalized;
            if (direction == Vector2.zero)
            {
                direction = Vector2.up;
            }

            if (Mathf.Abs(direction.y) < 0.22f)
            {
                direction.y = direction.y < 0f ? -0.22f : 0.22f;
                direction.Normalize();
            }

            _body.linearVelocity = direction * MovementSpeed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Brick brick = collision.collider.GetComponent<Brick>();
            if (brick != null)
            {
                _game.HitBrick(brick);
                return;
            }

            PaddleController paddle = collision.collider.GetComponent<PaddleController>();
            if (paddle != null && _body.linearVelocity.y < 0f)
            {
                float offset = (transform.position.x - paddle.transform.position.x) / (paddle.Width * 0.5f);
                _body.linearVelocity = new Vector2(Mathf.Clamp(offset, -0.94f, 0.94f), 1f).normalized * MovementSpeed;
            }

            _game.PlayBounce();
        }
    }

    public sealed class Brick : MonoBehaviour
    {
        private BreakoutGame _game;
        private SpriteRenderer _renderer;
        private int _health;
        private int _points;

        public void Initialize(BreakoutGame game, SpriteRenderer renderer, int health, int points)
        {
            _game = game;
            _renderer = renderer;
            _health = health;
            _points = points;
        }

        public void TakeHit()
        {
            _health--;
            if (_health <= 0)
            {
                _game.BrickDestroyed(_points);
                Destroy(gameObject);
                return;
            }

            _renderer.color = Color.Lerp(_renderer.color, Color.white, 0.42f);
            _game.BrickDamaged(_points / 2);
        }
    }

    public sealed class LoseZone : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            BallController ball = other.GetComponent<BallController>();
            if (ball != null)
            {
                FindFirstObjectByType<BreakoutGame>().LoseLife();
            }
        }
    }

    public sealed class EditorPreviewOnly : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.SetActive(false);
        }
    }
}
