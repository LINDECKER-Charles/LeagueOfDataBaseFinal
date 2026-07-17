<?php
declare(strict_types=1);

namespace App\Form;

use App\Entity\User;
use Symfony\Component\Form\AbstractType;
use Symfony\Component\Form\Extension\Core\Type\CheckboxType;
use Symfony\Component\Form\Extension\Core\Type\EmailType;
use Symfony\Component\Form\Extension\Core\Type\PasswordType;
use Symfony\Component\Form\Extension\Core\Type\TextType;
use Symfony\Component\Form\FormBuilderInterface;
use Symfony\Component\OptionsResolver\OptionsResolver;
use Symfony\Component\Validator\Constraints\IsTrue;
use Symfony\Component\Validator\Constraints\Length;
use Symfony\Component\Validator\Constraints\NotBlank;

final class RegistrationFormType extends AbstractType
{
    private const PASSWORD_MIN_LENGTH = 8;
    // bcrypt/argon inputs are capped upstream; 4096 guards against DoS-sized payloads.
    private const PASSWORD_MAX_LENGTH = 4096;

    public function buildForm(FormBuilderInterface $builder, array $options): void
    {
        $builder
            ->add('email', EmailType::class, [
                'label' => 'auth.register.email',
            ])
            ->add('username', TextType::class, [
                'label' => 'auth.register.username',
                'help' => 'auth.register.username_help',
            ])
            ->add('plainPassword', PasswordType::class, [
                'label' => 'auth.register.password',
                'mapped' => false,
                'attr' => ['autocomplete' => 'new-password'],
                'constraints' => [
                    new NotBlank(),
                    new Length(min: self::PASSWORD_MIN_LENGTH, max: self::PASSWORD_MAX_LENGTH),
                ],
            ])
            ->add('agreeTerms', CheckboxType::class, [
                'label' => 'auth.register.terms',
                'mapped' => false,
                'constraints' => [new IsTrue()],
            ]);
    }

    public function configureOptions(OptionsResolver $resolver): void
    {
        $resolver->setDefaults([
            'data_class' => User::class,
        ]);
    }
}
