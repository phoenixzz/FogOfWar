using UnityEngine;
using System.Collections;
using System;
using Game.Tools.FOV;

[RequireComponent(typeof(Projector))]
public class Fow : MonoBehaviour
{
    public uint ViewRadius = 5;
    public byte DarkFogGray = 32; 
    public bool ViewBeam = false;
    public fov_direction_type ViewBeamDirection = fov_direction_type.FOV_EAST;
    public float ViewBeamAngle = 130.0f;



    private fov.fov_settings_type fov_settings = new fov.fov_settings_type();

    private Transform m_Center = null;
    private Projector m_Projector = null;

    private fov m_BlockFov = new fov();
    private map m_BlockMap = null;



    private Texture2D m_MaskTex = null;
    private int m_TexWidth = 0;
    private int m_TexHeight = 0;


    private Color32[] m_TexColor = new Color32[0];

    private int m_OldgridX = 0;
    private int m_OldgridZ = 0;
    private float m_LastUpdateTime = 0;


    // Step 1 切换地图后首先调用，根据MaskBuffer数据 初始化Map
    public void InitMap(int width, int height, byte[] MaskBuffer)
    {
        m_TexWidth = width;
        m_TexHeight = height;
        m_BlockMap = new map(width, height);

        for (int y = 0; y < m_TexHeight; ++y)
        {
            for (int x = 0; x < m_TexWidth; ++x)
            {
                int number = x + m_TexWidth * y;
                int index = number / 8;
                int bit = number % 8;

                if (index < MaskBuffer.Length)
                {
                    bool bBlock = ((MaskBuffer[index] & (1 << bit)) > 0); // 大于0，表示有阻挡
                    m_BlockMap.initBlock(x, y, bBlock);
                }
            }
        }
    }

    // Step 2 显示FOW Enable Projector Component，初始化纹理全黑。(此步必须确保Projector已经加载至场景)
    public void StartFow(Transform center)
    {
        m_Center = center;

        //GameObject[] goes = GameObject.FindGameObjectsWithTag("FOW");
        //foreach (GameObject go in goes)
        //{
        //    m_Projector = go.GetComponent<Projector>();
        //    break;
        //}

        m_Projector = gameObject.GetComponent<Projector>();

        if (m_Projector != null)
        {
            m_MaskTex = new Texture2D(m_TexWidth, m_TexHeight, TextureFormat.RGBA32, false);
            m_MaskTex.wrapMode = TextureWrapMode.Clamp;
            
            m_TexColor = new Color32[m_TexWidth * m_TexHeight];
            for (int i = 0; i < m_TexWidth * m_TexHeight; ++i)
                m_TexColor[i] = new Color32(0, 0, 0, 255);
            m_MaskTex.SetPixels32(m_TexColor);
            m_MaskTex.Apply();

            m_Projector.enabled = true;
            m_Projector.material.SetTexture("_ShadowTex", m_MaskTex);
        }
    }

    // Step 3 结束FOW Disable Projector Component，清空纹理
    public void EndFow()
    {
        m_Center = null;

        if (m_Projector != null)
        {
            // 清空工作纹理
            m_Projector.material.SetTexture("_ShadowTex", null);
            m_Projector.enabled = false;
            m_MaskTex = null;
        }
    }



    //void Awake()
    //{
    //}
    void Start()
    {
        m_BlockFov.fov_settings_init(fov_settings);
        m_BlockFov.fov_settings_set_opacity_test_function(fov_settings, opaque);
        m_BlockFov.fov_settings_set_apply_lighting_function(fov_settings, apply);
    }

    void FixedUpdate()
    {
        if (m_Center != null && m_Projector != null)
        {
            int gridX = Mathf.FloorToInt(m_Center.position.x * 2);
            int gridZ = Mathf.FloorToInt(m_Center.position.z * 2);

            // 0.5s 刷新一次
            if ((gridX != m_OldgridX || gridZ != m_OldgridZ) || (Time.fixedTime - m_LastUpdateTime > 0.5f))
            {
                m_OldgridX = gridX;
                m_OldgridZ = gridZ;
                m_LastUpdateTime = Time.fixedTime;


                /* Cause libfov to mark lit cells using the two callbacks given:
                 *  - opaque, which is used to determine information about your
                 *    map (a query you can define), and
                 *  - apply, which is used to modify your map once libfov
                 *    determines that a particular cell is lit (a command you can
                 *    define).
                 *
                 *  In this call, the light source is at (pX,pY). 
                 */
                if (ViewBeam)
                {
                    float angle_n = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), Vector2.up);
                    float angle_e = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), Vector2.right);
                    float angle_w = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), Vector2.left);
                    float angle_s = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), Vector2.down);

                    float angle_ne = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), new Vector2(1, 1));
                    float angle_se = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), new Vector2(1, -1));
                    float angle_sw = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), new Vector2(-1, -1));
                    float angle_nw = Vector2.Angle(new Vector2(m_Center.forward.x, m_Center.forward.z), new Vector2(-1, 1));

                    if (angle_s <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_NORTH;
                    else if (angle_e <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_EAST;
                    else if (angle_w <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_WEST;
                    else if (angle_n <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_SOUTH;
                    else if (angle_se <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_NORTHEAST;
                    else if (angle_ne <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_SOUTHEAST;
                    else if (angle_nw <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_SOUTHWEST;
                    else if (angle_sw <= 22.5f)
                        ViewBeamDirection = fov_direction_type.FOV_NORTHWEST;

                    m_BlockFov.fov_beam(fov_settings, m_BlockMap, Color.white, m_OldgridX, m_OldgridZ, ViewRadius, ViewBeamDirection, ViewBeamAngle);
                }
                else
                {
                    m_BlockFov.fov_circle(fov_settings, m_BlockMap, Color.white, m_OldgridX, m_OldgridZ, ViewRadius);
                }

                for (int y = 0; y < m_TexHeight; ++y)
                {
                    for (int x = 0; x < m_TexWidth; ++x)
                    {
                        if (m_BlockMap.isSeen(x, y))
                            m_TexColor[x + y * m_TexWidth] = new Color32(255, m_TexColor[x + y * m_TexWidth].r, 255, 255);
                        else if (m_BlockMap.isRemembered(x, y))
                            m_TexColor[x + y * m_TexWidth] = new Color32(DarkFogGray, m_TexColor[x + y * m_TexWidth].r, DarkFogGray, 255);
                        else
                            m_TexColor[x + y * m_TexWidth] = new Color32(0, m_TexColor[x + y * m_TexWidth].r, 0, 255);

                        m_BlockMap.setUnSeen(x, y);
                    }
                }

                m_TexColor[gridX + gridZ * m_TexWidth] = new Color32(255, 255, 255, 255);

                m_MaskTex.SetPixels32(m_TexColor);
                m_MaskTex.Apply();
                m_Projector.material.SetFloat("_StartTime", Time.timeSinceLevelLoad);
            }

            
        }
    }
    //void Update()
    //{
        
    //}
    //void LateUpdate()
    //{
       
    //}

    /* Callbacks ------------------------------------------------------ */

    /**
     * Function called by libfov to apply light to a cell.
     *
     * \param map map data structure passed to function such as  fov_circle.
     * \param x   Absolute x-axis position of cell.
     * \param y   Absolute x-axis position of cell.
     * \param dx  Offset of cell from source cell on x-axis.
     * \param dy  Offset of cell from source cell on y-axis.
     * \param src source data structure passed to function such as  fov_circle.
     */
    private void apply(map _map, int x, int y, int dx, int dy, Color LihghtSrc)
    {
        if (_map.onMap(x, y))
        {
            _map.setSeen(x, y);
        }
    }


    /**
     * Function called by libfov to determine whether light can pass
     * through a cell. Return zero if light can pass though the cell at
     * (x,y), non-zero if it cannot.
     *
     * \param map  map data structure passed to function such as  fov_circle.
     * \param x   Absolute x-axis position of cell.
     * \param y   Absolute x-axis position of cell.
     */
    private bool opaque(map _map, int x, int y)
    {
        return _map.blockLOS(x, y);
    }
}
