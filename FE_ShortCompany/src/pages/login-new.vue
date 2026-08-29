<script setup>
import { useGenerateImageVariant } from '@core/composable/useGenerateImageVariant'

import authV2LoginIllustrationBorderedDark from '@images/pages/auth-v2-login-illustration-bordered-dark.png'
import authV2LoginIllustrationBorderedLight from '@images/pages/auth-v2-login-illustration-bordered-light.png'
import authV2LoginIllustrationDark from '@images/pages/auth-v2-login-illustration-dark.png'
import authV2LoginIllustrationLight from '@images/pages/auth-v2-login-illustration-light.png'
import authV2MaskDark from '@images/pages/misc-mask-dark.png'
import authV2MaskLight from '@images/pages/misc-mask-light.png'
import { jwtDecode } from 'jwt-decode'

document.title = "بيانات التطبيق | تسجيل الدخول"

definePage({ meta: { layout: 'blank' } })

const form = ref({
  userName: '',
  password: '',
  userType: 'مستخدم',
})

const baseUrl = import.meta.env.VITE_API_BASE_URL

const errorMessage = ref("")
const showAlert = ref(false)
const refForm = ref({})

const validateForm = async () => {
  return await refForm.value?.validate()
}

const isPasswordVisible = ref(false)
const authThemeImg = useGenerateImageVariant(authV2LoginIllustrationLight, authV2LoginIllustrationDark, authV2LoginIllustrationBorderedLight, authV2LoginIllustrationBorderedDark, true)
const authThemeMask = useGenerateImageVariant(authV2MaskLight, authV2MaskDark)


async function login() {
  showAlert.value = false 

  const validForm = await validateForm()

  if (validForm.valid === true) {
    fetch(`${baseUrl}/api/User/LoginUser`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(form.value),
    })
      .then(res=> res.json())
      .then(resData => {
        if (resData.message) {
          showAlert.value = true
          errorMessage.value = resData.message
        } else {

          const user = jwtDecode(resData.token)
          
          localStorage.setItem("token", resData.token)
          localStorage.setItem('expireTime', user.exp)
          localStorage.setItem('UserID', user.UserID)
          localStorage.setItem('UserName', user.UserName)
          localStorage.setItem('FullName', user.FullName)
          localStorage.setItem('Image', user.Image)
          localStorage.setItem('UserPermissions', user.UserPermissions)

          window.location.href = '/'
        }
      })
      .catch(err => {
        showAlert.value = true
        errorMessage.value = err.message
      })
  } else {
    showAlert.value = true
    errorMessage.value = "يجب ادخال اسم المستخدم وكلمة المرور"
  }
}
</script>

<template>
  <div class="background ">
    <div class="container">
      <div class="head-logo-section">
        <img
          src="../assets/images/logo-qoute.png"
          alt="arch_logo"
        >
        <img
          src="../assets/images/logo_vision.png"
          alt="arch_logo"
        >
      </div>
      <div class="main-logo-section">
        <div>
          <h1>
            logo
          </h1>
        </div>
      </div>
      <div class="form-section">
        <form onsubmit="">
          <input
            id="username"
            class="login-text-field"
            type="text"
            name="username"
            placeholder="اسم المستخدم"
          >
          <input
            id="password"
            type="password"
            name="password"
            class="login-text-field"
            placeholder="*******"
          >
          <button
            type="submit"
            class="login-btn"
          >
            تسجيل
          </button>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
  .background {
    height: 100vh; 
    width: 100vw;
    top: 0;
    right: 0;
    background-image: url("../assets/images/background.png");
    background-position: center;
  }

  .head-logo-section {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-top: 80px;
  }
  .head-logo-section img {
    width: 300px;
  }
  .main-logo-section{
    display: flex;
    align-items: center;
    justify-content: center;
    padding-top: 100px;
  }
  .main-logo-section > div{
    width: 330px;
    height: 135px;
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: #fff;
    border-radius: 20px;
  }

  .main-logo-section > div > h1 {
    font-size: 50px;
    color: #a1a1a1;
  }

  .form-section{
    display: flex;
    align-items: center;
    justify-content: center;
    flex-direction: column;
  }

  .form-section > form{
    display: flex;
    align-items: center;
    justify-content: center;
    flex-direction: column;
    margin-top: 100px;
  }

  .login-text-field {
    width: 700px;
    height: 75px;
    margin-bottom: 30px;
    background-color: #fff;
    border-radius: 1000px;
    outline: none;
    padding: 0 30px;
    font-size: 25px;
  }

  .login-text-field :focus {
    background-color: #e6e4e4;
  }

  .login-btn {
    padding: 10px 20px;
    font-size: 35px;
    width: 200px;
    border-radius: 1000px;
    background-color: #f4ca6f;
  }




  
  .container {
    position: relative;
    width: 100%;
    padding-right: var(--bs-gutter-x, 1rem);
    padding-left: var(--bs-gutter-x, 1rem);
    margin-right: auto;
    margin-left: auto;
  }

  @media (min-width: 576px) {
    .container {
      max-width: 540px;
    }
  }

  @media (min-width: 768px) {
    .container {
      max-width: 720px;
    }
  }

  @media (min-width: 992px) {
    .container {
      max-width: 960px;
    }
  }

  @media (min-width: 1200px) {
    .container {
      max-width: 1140px;
    }
  }

  @media (min-width: 1400px) {
    .container {
      max-width: 1320px;
    }
  }
</style>
