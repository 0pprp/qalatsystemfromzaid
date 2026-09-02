<script setup>
import axios from "axios"
import { jwtDecode } from "jwt-decode"
import { computed, ref } from 'vue'
import { useCities, isLocalLab, isDemo, LOCAL_API, DEMO_API } from '@/composables/useCities'
import { useUserRole } from '@/composables/useUserRole'

const userName = ref(localStorage.getItem("UserName")) || ''
const password = ref(localStorage.getItem("Password")) || ''
const UserImage = localStorage.getItem("UserImage") || ''
const apiUrl = localStorage.getItem('LinkCity')
const apiUrlImage = apiUrl ? apiUrl.replace('api/', 'Images/') : ''
const imageFinal = apiUrlImage+UserImage
const errorMessage = ref("")
const showAlert = ref(false)

const { provinces } = useCities()
const { canSwitchCity, userType } = useUserRole()

const roleLabel = computed(() => {
  if (userType.value === 'مدير فرع') return 'مدير فرع'
  if (userType.value === 'محاسب رئيسي') return 'ادمن'
  if (userType.value === 'محاسب فرعي') return 'محاسب فرعي'

  return userType.value || 'ادمن'
})

const changeCity = async city => {
  if (!canSwitchCity.value) {
    showAlert.value = true
    errorMessage.value = "مدير الفرع لا يمكنه تبديل المحافظة"

    return
  }

  const selectedApi = isDemo()
    ? DEMO_API
    : (isLocalLab() ? LOCAL_API : city.link)
  const cityName = city.name
  const database = city.database

  if (!selectedApi) {
    showAlert.value = true
    errorMessage.value = "لا يوجد رابط API لهذه المحافظة"

    return
  }


  const response = await axios.post(
    `${selectedApi}Users/Users_LoginAdmin`,
    {
      userName: userName.value,
      password: password.value,
    },
    {
      headers: {
        "Content-Type": "application/json",
      },
    },
  )

  const resData = response.data
  const user = jwtDecode(resData.token)
  if(resData.token.length>0){
    localStorage.setItem("Token", resData.token)
    localStorage.setItem('Expiration', resData.expiration)
    localStorage.setItem('UserID', user.UserID)
    localStorage.setItem('UserImage', user.UserImage)
    localStorage.setItem('UserType', user.UserType || user.userType || localStorage.getItem('UserType') || '')
    localStorage.setItem("LinkCity", selectedApi)
    localStorage.setItem("CityName", cityName)
    localStorage.setItem("Database", database)
    location.reload()
  }
}
</script>

<template>
  <VBadge
    dot
    location="bottom right"
    offset-x="3"
    offset-y="3"
    bordered
    color="success"
  >
    <VAvatar
      class="cursor-pointer"
      color="primary"
      variant="tonal"
    >
      <VImg
        v-if="UserImage && !UserImage.includes(':')"
        :src="imageFinal"
        @error="$event.target.src = 'https://www.w3schools.com/w3images/avatar2.png'"
      >
        <template #placeholder>
          <VIcon
            icon="tabler-user"
            size="30"
          />
        </template>
        <template #error>
          <VIcon
            icon="tabler-user"
            size="30"
          />
        </template>
      </VImg>
      <VIcon
        v-else
        icon="tabler-user"
        size="30"
      />

      <!-- SECTION Menu -->
      <VMenu
        activator="parent"
        width="230"
        location="bottom end"
        offset="14px"
      >
        <VList>
          <!-- 👉 User Avatar & Name -->
          <VListItem>
            <template #prepend>
              <VListItemAction start>
                <VBadge
                  dot
                  location="bottom right"
                  offset-x="3"
                  offset-y="3"
                  color="success"
                >
                  <VAvatar
                    color="primary"
                    variant="tonal"
                  >
                    <VImg
                      v-if="UserImage && !UserImage.includes(':')"
                      :src="imageFinal"
                      @error="$event.target.src = 'https://www.w3schools.com/w3images/avatar2.png'"
                    >
                      <template #placeholder>
                        <VIcon icon="tabler-user" />
                      </template>
                      <template #error>
                        <VIcon icon="tabler-user" />
                      </template>
                    </VImg>
                    <VIcon
                      v-else
                      icon="tabler-user"
                    />
                  </VAvatar>
                </VBadge>
              </VListItemAction>
            </template>

            <VListItemTitle class="font-weight-semibold">
              {{ userName }}
            </VListItemTitle>
            <VListItemSubtitle>{{ roleLabel }}</VListItemSubtitle>
          </VListItem>

          <VDivider class="my-2" />

          <!-- Divider -->

          <!-- قائمة المدن ديناميكية من API — مدير الفرع مربوط بفرعه -->
          <template v-if="canSwitchCity">
            <VListItem
              v-for="city in provinces"
              :key="city.value"
              @click="changeCity(city)"
            >
              <template #prepend>
                <VIcon
                  class="me-2"
                  icon="tabler-map"
                  size="22"
                />
              </template>
              <VListItemTitle>{{ city.name }}</VListItemTitle>
            </VListItem>
          </template>
          <!-- تسجيل الخروج -->
          <VDivider class="my-2" />

          <VListItem to="/logout">
            <template #prepend>
              <VIcon
                class="me-2"
                icon="tabler-logout"
                size="22"
              />
            </template>
            <VListItemTitle>تسجيل الخروج</VListItemTitle>
          </VListItem>
        </VList>
      </VMenu>
      <!-- !SECTION -->
    </VAvatar>
  </VBadge>
</template>
