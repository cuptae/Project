using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Animations.Rigging;
public class Gunner : PlayerCtrl
{
    public GameObject bulletEffect;
    public GameObject gunFire;
    private Rig aimRig;
    private Ray ray;
    MultiAimConstraint multiAimConstraint;
    private float nextFireTime = 0f;
    private Transform aimingPos;
    public Transform firePos;
    public float attackDistance;
    public float attackRange;
    

    protected override void Awake()
    {
        base.Awake();
        characterStat.GetCharacterDataByName("Gunner");
        aimRig = GetComponentInChildren<Rig>();
        multiAimConstraint = GetComponentInChildren<MultiAimConstraint>();
        aimingPos = GameObject.FindWithTag("AimingPos").transform;
        SetHPInit(characterStat.MaxHp);
    }

    protected override void Start()
    {
        base.Start();
        WeightedTransformArray sourceObjects = multiAimConstraint.data.sourceObjects;
        sourceObjects.Add(new WeightedTransform(aimingPos,aimRig.weight));
        if(pv.isMine)
        {
            PoolManager.Instance.CreatePool(bulletEffect.name, bulletEffect, 30);
            PoolManager.Instance.CreatePool(gunFire.name,gunFire,30);
        }
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
        
        if(pv.isMine)
        {
            FireAnim();
            UniqueAbility();
        }
    }

    void FireAnim()
    {
        if (Input.GetMouseButton(0))
        {
            isAttack = true;
            SetRigWeight(1);
            animator.SetBool("Attack", true);
        }
        else
        {
            isAttack = false;
            SetRigWeight(0);
            animator.SetBool("Attack", false);
        }
    }

    void SetRigWeight(float weight)
    {
        aimRig.weight = weight;
    }

    public override void Attack()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {

            ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            Vector3 direction = ray.direction.normalized;
            ray = new Ray(firePos.position, direction);
            FireEffect();
            if (Physics.Raycast(ray, out RaycastHit hitInfo, 50.0f))
            {
                aimingPos.transform.position = hitInfo.point + Vector3.up * 0.5f;
                // 부모에서 EnemyCtrl을 찾도록 수정
                var enemy = hitInfo.collider.GetComponent<IDamageable>();
                
                Quaternion hitDir = Quaternion.LookRotation(-direction);
                if (enemy != null) // 적 오브젝트를 찾았다면
                {
                    enemy.GetDamage(characterStat.Damage);
                    BulletEffect(hitInfo.point, hitDir);
                }
            }
            SoundManager.Instance.PlaySFX(SFXCategory.GUNNER, PLAYER.ATTACK, tr.position);
            nextFireTime = Time.time + characterStat.AttackRate; // 다음 발사 시간 설정
        }
    }
    

    public override void UniqueAbility()
    {
        if(Input.GetMouseButtonDown(1)&&canAbility)
        {
            ModifyDamage(characterStat.Damage/2);
            ModifyAttackRate(-characterStat.AttackRate*0.5f);
            canAbility = false;
            StartCoroutine(HandleAbilityCooldown());
        }
        
    }
    private IEnumerator HandleAbilityCooldown()
    {
        // 게이지를 감소시킴
        yield return StartCoroutine(ReduceAbilityBar());

        // 게이지를 천천히 증가시킴
        yield return StartCoroutine(AbilityCoolTime(13f));
    }
    IEnumerator ReduceAbilityBar()
    {
        while (abilitycooldownbar.fillAmount > 0f)
        {
            abilitycooldownbar.fillAmount = Mathf.MoveTowards(abilitycooldownbar.fillAmount,0f,Time.deltaTime * 0.25f);
            yield return null;
        }
        abilitycooldownbar.fillAmount = 0f; // 최종적으로 0으로 설정
        characterStat.modifyDamage = 0;
        characterStat.modifyAttackRate = 0;
        // 능력 사용 후 상태 초기화
    }
    IEnumerator AbilityCoolTime(float cooltime)
    {
        float elapsedTime = 0f;
        while (elapsedTime < cooltime)
        {
            elapsedTime += Time.deltaTime;
            abilitycooldownbar.fillAmount = elapsedTime / cooltime; // 천천히 증가
            yield return null;
        }
        abilitycooldownbar.fillAmount = 1f; // 최종적으로 1로 설정
 
        canAbility = true; // 능력을 다시 사용할 수 있도록 설정
    }

    protected override void  OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        base.OnPhotonSerializeView(stream,info);
        if(stream.isWriting)
        {
            stream.SendNext(isAttack);
            stream.SendNext(animator.GetBool("Attack"));
        }
        else
        {
            isAttack = (bool)stream.ReceiveNext();
            animator.SetBool("Attack",(bool)stream.ReceiveNext());
        }
    }

    void BulletEffect(Vector3 pos, Quaternion rot)
    {
        GameObject effect = PoolManager.Instance.GetObject(bulletEffect.name,pos, rot);
    }
    void FireEffect()
    {
        GameObject gunFire = PoolManager.Instance.GetObject(this.gunFire.name,firePos.position,Quaternion.LookRotation(firePos.forward));
    }

    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR

            // 공격 범위 세팅
            Vector3 boxRange = new Vector3(attackRange, 3f, attackDistance);
            Vector3 boxCenter = new Vector3(0, -1, -1 + boxRange.z / 2f);
            Vector3 attackPos = transform.position + transform.forward * 2f + transform.up * 2f;
            Quaternion attackRot = transform.rotation;

            // 최종 중심 계산
            Vector3 center = attackPos + attackRot * boxCenter;

            // 회전 행렬 적용
            Gizmos.color = Color.red;
            Matrix4x4 rotMatrix = Matrix4x4.TRS(center, attackRot, Vector3.one);
            Gizmos.matrix = rotMatrix;

            Gizmos.DrawWireCube(Vector3.zero, boxRange);
        #endif
    }
}


