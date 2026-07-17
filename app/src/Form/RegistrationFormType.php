<?php
declare(strict_types=1);

namespace App\Form;

use App\Entity\User;
use Symfony\Component\Form\AbstractType;
use Symfony\Component\Form\Extension\Core\Type\CheckboxType;
use Symfony\Component\Form\Extension\Core\Type\EmailType;
use Symfony\Component\Form\Extension\Core\Type\RepeatedType;
use Symfony\Component\Form\Extension\Core\Type\TextType;
use Symfony\Component\Form\FormBuilderInterface;
use Symfony\Component\OptionsResolver\OptionsResolver;
use Symfony\Component\Validator\Constraints\IsTrue;
use Symfony\Contracts\Translation\TranslatorInterface;

final class RegistrationFormType extends AbstractType
{
    public function __construct(private readonly TranslatorInterface $translator)
    {
    }

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
            // CNIL policy + confirmation; shared with the profile set-password flow.
            ->add('plainPassword', RepeatedType::class, PasswordFieldOptions::repeated($this->translator))
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
