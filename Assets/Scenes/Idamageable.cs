public interface IDamageable
{
    void TakeDamage(int amount);
    void TakeDamage(float amount);
    bool IsDead();
}